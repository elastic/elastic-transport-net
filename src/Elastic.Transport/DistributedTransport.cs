// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Elastic.Transport.Diagnostics;
using Elastic.Transport.Diagnostics.Auditing;
using Elastic.Transport.Extensions;
using Elastic.Transport.Products;

#if NETFRAMEWORK
using System.Net;
#endif

namespace Elastic.Transport;

/// <summary>
/// Transport coordinates the client requests over the node pool nodes and is in charge of falling over on
/// different nodes
/// </summary>
/// <param name="configuration">The configuration to use for this transport</param>
public sealed class DistributedTransport(ITransportConfiguration configuration) : DistributedTransport<ITransportConfiguration>(configuration);

/// <summary>
/// Transport coordinates the client requests over the node pool nodes and is in charge of falling over on
/// different nodes
/// </summary>
public class DistributedTransport<TConfiguration> : ITransport<TConfiguration>
	where TConfiguration : class, ITransportConfiguration
{
	private readonly ProductRegistration _productRegistration;

	private ConditionalWeakTable<RequestConfiguration, BoundConfiguration>? _boundConfigurations;

	/// <summary>
	/// Transport coordinates the client requests over the node pool nodes and is in charge of falling over on
	/// different nodes
	/// </summary>
	/// <param name="configuration">The configuration to use for this transport</param>
	public DistributedTransport(TConfiguration configuration)
	{
		configuration.ThrowIfNull(nameof(configuration));
		configuration.NodePool.ThrowIfNull(nameof(configuration.NodePool));
		configuration.RequestInvoker.ThrowIfNull(nameof(configuration.RequestInvoker));
		configuration.RequestResponseSerializer.ThrowIfNull(nameof(configuration.RequestResponseSerializer));

		_productRegistration = configuration.ProductRegistration;
		Configuration = configuration;
		TransportBoundConfiguration = new BoundConfiguration(Configuration);
		TransportPipeline = Configuration.PipelineProvider.Create(TransportBoundConfiguration);
	}

	private RequestPipeline TransportPipeline { get; }
	private BoundConfiguration TransportBoundConfiguration { get; }

	/// <inheritdoc cref="ITransport{TConfiguration}.Configuration"/>
	public TConfiguration Configuration { get; }

	ITransportConfiguration ITransport.Configuration => Configuration;

	/// <inheritdoc cref="ITransport.Request{TResponse}"/>
	public TResponse Request<TResponse>(
		in EndpointPath path,
		PostData? data,
		Action<Activity>? configureActivity,
		IRequestConfiguration? localConfiguration
	) where TResponse : TransportResponse, new() =>
		RequestCoreAsync<TResponse>(isAsync: false, path, data, configureActivity, localConfiguration)
			.EnsureCompleted();

	/// <inheritdoc cref="ITransport.RequestAsync{TResponse}"/>
	public Task<TResponse> RequestAsync<TResponse>(
		in EndpointPath path,
		PostData? data,
		Action<Activity>? configureActivity,
		IRequestConfiguration? localConfiguration,
		CancellationToken cancellationToken = default
	) where TResponse : TransportResponse, new() =>
		RequestCoreAsync<TResponse>(isAsync: true, path, data, configureActivity, localConfiguration, cancellationToken)
			.AsTask();

	private async ValueTask<TResponse> RequestCoreAsync<TResponse>(
		bool isAsync,
		EndpointPath path,
		PostData? data,
		Action<Activity>? configureActivity,
		IRequestConfiguration? localConfiguration,
		CancellationToken cancellationToken = default
	) where TResponse : TransportResponse, new()
	{
		Activity activity = null;

		if (OpenTelemetry.ElasticTransportActivitySource.HasListeners())
			activity = OpenTelemetry.ElasticTransportActivitySource.StartActivity(path.Method.GetStringValue(),
				ActivityKind.Client);

		try
		{
			var boundConfiguration = BindConfiguration(localConfiguration);

			Configuration.OnConfigurationBound?.Invoke(boundConfiguration);

			var pipeline = boundConfiguration == TransportBoundConfiguration ? TransportPipeline : Configuration.PipelineProvider.Create(boundConfiguration);
			var startedOn = Configuration.DateTimeProvider.Now();
			var auditor = boundConfiguration.DisableAuditTrail ? null : new Auditor(Configuration.DateTimeProvider);

			if (isAsync)
				await pipeline.FirstPoolUsageAsync(Configuration.BootstrapLock, auditor, cancellationToken).ConfigureAwait(false);
			else
				pipeline.FirstPoolUsage(Configuration.BootstrapLock, auditor);

			TResponse response = null;

			var endpoint = Endpoint.Empty(path);

			if (activity is { IsAllDataRequested: true })
			{
				if (Configuration.Authentication is BasicAuthentication basicAuthentication)
					activity.SetTag(SemanticConventions.DbUser, basicAuthentication.Username);

				activity.SetTag(OpenTelemetryAttributes.ElasticTransportProductName, Configuration.ProductRegistration.Name);
				activity.SetTag(OpenTelemetryAttributes.ElasticTransportProductVersion, Configuration.ProductRegistration.ProductAssemblyVersion);
				activity.SetTag(OpenTelemetryAttributes.ElasticTransportVersion, ReflectionVersionInfo.TransportVersion);
				activity.SetTag(SemanticConventions.UserAgentOriginal, Configuration.UserAgent.ToString());
				activity.SetTag(SemanticConventions.HttpRequestMethod, endpoint.Method.GetStringValue());
			}

			List<PipelineException>? seenExceptions = null;
			var attemptedNodes = 0;

			if (pipeline.TryGetSingleNode(out var singleNode))
			{
				endpoint = endpoint with { Node = singleNode };
				// No value in marking a single node as dead. We have no other options!
				attemptedNodes = 1;
				activity?.SetTag(SemanticConventions.UrlFull, endpoint.Uri.AbsoluteUri);
				activity?.SetTag(SemanticConventions.ServerAddress, endpoint.Uri.Host);
				activity?.SetTag(SemanticConventions.ServerPort, endpoint.Uri.Port);

				for (var attempt = 0; attempt < 2; attempt++)
				{
					try
					{
						if (isAsync)
							response = await pipeline.CallProductEndpointAsync<TResponse>(endpoint, boundConfiguration, data, auditor, cancellationToken)
								.ConfigureAwait(false);
						else
							response = pipeline.CallProductEndpoint<TResponse>(endpoint, boundConfiguration, data, auditor);
					}
					catch (PipelineException pipelineException) when (!pipelineException.Recoverable)
					{
						HandlePipelineException(ref response, pipelineException, pipeline, singleNode, ref seenExceptions);
						break;
					}
					catch (PipelineException pipelineException)
					{
						HandlePipelineException(ref response, pipelineException, pipeline, singleNode, ref seenExceptions);
						break;
					}
					catch (Exception killerException)
					{
						ThrowUnexpectedTransportException(killerException, seenExceptions, endpoint, response, auditor);
					}

					// Retry once when a pooled connection was closed by the server/LB
					// before we used it. SocketsHttpHandler classifies this as InvalidResponse
					// rather than a connection error, so it does not auto-retry internally.
					// The stale connection is purged after the first failure; the retry
					// succeeds on a fresh connection.
					if (attempt == 0 && IsStaleConnectionException(response))
					{
						attemptedNodes++;
						continue;
					}

					break;
				}
			}
			else
			{
				foreach (var node in pipeline.NextNode(startedOn, attemptedNodes, auditor))
				{
					attemptedNodes++;
					endpoint = endpoint with { Node = node };

					// If multiple nodes are attempted, the final node attempted will be used to set the operation span attributes.
					// Each physical node attempt in CallProductEndpoint will also record these attributes.
					activity?.SetTag(SemanticConventions.UrlFull, endpoint.Uri.AbsoluteUri);
					activity?.SetTag(SemanticConventions.ServerAddress, endpoint.Uri.Host);
					activity?.SetTag(SemanticConventions.ServerPort, endpoint.Uri.Port);

					try
					{
						if (_productRegistration.SupportsSniff)
						{
							if (isAsync)
								await pipeline.SniffOnStaleClusterAsync(auditor, cancellationToken).ConfigureAwait(false);
							else
								pipeline.SniffOnStaleCluster(auditor);
						}
						if (_productRegistration.SupportsPing)
						{
							if (isAsync)
								await PingAsync(pipeline, node, auditor, cancellationToken).ConfigureAwait(false);
							else
								Ping(pipeline, node, auditor);
						}

						if (isAsync)
							response = await pipeline.CallProductEndpointAsync<TResponse>(endpoint, boundConfiguration, data, auditor, cancellationToken)
								.ConfigureAwait(false);
						else
							response = pipeline.CallProductEndpoint<TResponse>(endpoint, boundConfiguration, data, auditor);

						if (!response.ApiCallDetails.SuccessOrKnownError)
						{
							pipeline.MarkDead(node);

							if (_productRegistration.SupportsSniff)
							{
								if (isAsync)
									await pipeline.SniffOnConnectionFailureAsync(auditor, cancellationToken).ConfigureAwait(false);
								else
									pipeline.SniffOnConnectionFailure(auditor);
							}
						}
					}
					catch (PipelineException pipelineException) when (!pipelineException.Recoverable)
					{
						HandlePipelineException(ref response, pipelineException, pipeline, node, ref seenExceptions);
						break;
					}
					catch (PipelineException pipelineException)
					{
						HandlePipelineException(ref response, pipelineException, pipeline, node, ref seenExceptions);
					}
					catch (Exception killerException)
					{
						if (killerException is OperationCanceledException && cancellationToken.IsCancellationRequested)
							pipeline.AuditCancellationRequested(auditor);

						throw new UnexpectedTransportException(killerException, seenExceptions)
						{
							Endpoint = endpoint,
							ApiCallDetails = response?.ApiCallDetails,
							AuditTrail = auditor
						};
					}

					if (cancellationToken.IsCancellationRequested)
					{
						pipeline.AuditCancellationRequested(auditor);
						break;
					}

					if (response == null || !response.ApiCallDetails.SuccessOrKnownError) continue;

					pipeline.MarkAlive(node);
					break;
				}
			}

			if (response is not null)
			{
#if NET6_0_OR_GREATER
				activity?.SetStatus(response.ApiCallDetails.HasSuccessfulStatusCodeAndExpectedContentType ? ActivityStatusCode.Ok : ActivityStatusCode.Error);
#endif
				activity?.SetTag(SemanticConventions.HttpResponseStatusCode, response.ApiCallDetails.HttpStatusCode);
			}
			activity?.SetTag(OpenTelemetryAttributes.ElasticTransportAttemptedNodes, attemptedNodes);

			// We don't check IsAllDataRequested here as that's left to the consumer.
			if (configureActivity is not null && activity is not null)
				configureActivity.Invoke(activity);

			if (activity is { IsAllDataRequested: true })
				OpenTelemetry.SetCommonAttributes(activity, Configuration);

			return FinalizeResponse(endpoint, boundConfiguration, data, pipeline, startedOn, attemptedNodes, auditor, seenExceptions, response);
		}
		finally
		{
			activity?.Dispose();
		}
	}

	private BoundConfiguration BindConfiguration(IRequestConfiguration? localConfiguration)
	{
		// Unless per request configuration is provided, we can reuse a BoundConfiguration
		// that is specific to this transport. If the IRequestConfiguration is an instance
		// of BoundConfiguration we use that cached instance directly without rebinding.
		return localConfiguration switch
		{
			BoundConfiguration bc => bc,
			RequestConfiguration rc => GetOrCreateBoundConfiguration(rc),
			not null => new BoundConfiguration(Configuration, localConfiguration),
			_ => TransportBoundConfiguration
		};

		BoundConfiguration GetOrCreateBoundConfiguration(RequestConfiguration rc)
		{
			// Cache `BoundConfiguration` for requests with local request configuration.

			// Since `IRequestConfiguration` might be implemented as mutable class, we use the
			// cache only with the immutable `RequestConfiguration` record.

			// ReSharper disable InconsistentlySynchronizedField

			var cache = (Interlocked.CompareExchange(
				ref _boundConfigurations,
				new ConditionalWeakTable<RequestConfiguration, BoundConfiguration>(),
				null
			) ?? _boundConfigurations)!;

			if (cache.TryGetValue(rc, out var boundConfiguration))
			{
				return boundConfiguration;
			}

#if NET8_0_OR_GREATER
			boundConfiguration = new BoundConfiguration(Configuration, rc);

			cache.TryAdd(rc, boundConfiguration);
#else
			lock (cache)
			{
				if (cache.TryGetValue(rc, out boundConfiguration))
				{
					return boundConfiguration;
				}

				boundConfiguration = new BoundConfiguration(Configuration, rc);

				cache.Add(rc, boundConfiguration);
			}
#endif

			// ReSharper restore InconsistentlySynchronizedField

			return boundConfiguration;
		}
	}

	private static void ThrowUnexpectedTransportException<TResponse>(Exception killerException,
		List<PipelineException> seenExceptions,
		Endpoint endpoint,
		TResponse response, IReadOnlyCollection<Audit>? auditTrail
	) where TResponse : TransportResponse, new() =>
		throw new UnexpectedTransportException(killerException, seenExceptions)
		{
			Endpoint = endpoint,
			ApiCallDetails = response?.ApiCallDetails,
			AuditTrail = auditTrail
		};

	private static void HandlePipelineException<TResponse>(
		ref TResponse response, PipelineException ex, RequestPipeline pipeline, Node node,
		ref List<PipelineException> seenExceptions
	)
		where TResponse : TransportResponse, new()
	{
		response ??= ex.Response as TResponse;
		pipeline.MarkDead(node);
		seenExceptions ??= new List<PipelineException>(1);
		seenExceptions.Add(ex);
	}

	/// <summary>
	/// Detects responses caused by a pooled HTTP connection that was silently closed by the
	/// server or an intermediate load balancer. <c>SocketsHttpHandler</c> surfaces this as
	/// an <c>HttpRequestException</c> with "Received an invalid status line:" rather than a
	/// connection-level error, so its built-in retry does not fire. A single transport-level
	/// retry on the same node is safe because the dead connection is already purged from the
	/// pool after the first failure.
	/// </summary>
	private static bool IsStaleConnectionException(TransportResponse? response)
	{
		var ex = response?.ApiCallDetails?.OriginalException;
		while (ex is not null)
		{
			if (ex.Message.Contains("Received an invalid status line:"))
				return true;
			ex = ex.InnerException;
		}
		return false;
	}

	private TResponse FinalizeResponse<TResponse>(
		Endpoint endpoint,
		BoundConfiguration boundConfiguration,
		PostData? postData,
		RequestPipeline pipeline,
		DateTimeOffset startedOn,
		int attemptedNodes,
		Auditor auditor,
		List<PipelineException>? seenExceptions,
		TResponse? response
	) where TResponse : TransportResponse, new()
	{
		if (endpoint.IsEmpty) //foreach never ran
			pipeline.ThrowNoNodesAttempted(endpoint, auditor, seenExceptions);

		var callDetails = GetMostRecentCallDetails(response, seenExceptions);
		var clientException = pipeline.CreateClientException(response, callDetails, endpoint, auditor, startedOn, attemptedNodes, seenExceptions);

		if (response?.ApiCallDetails == null)
			pipeline.BadResponse(ref response, callDetails, endpoint, boundConfiguration, postData, clientException, auditor);

		HandleTransportException(boundConfiguration, clientException, response);
		return response;
	}

	private static ApiCallDetails? GetMostRecentCallDetails<TResponse>(TResponse? response,
		IEnumerable<PipelineException>? seenExceptions
	)
		where TResponse : TransportResponse, new()
	{
		var callDetails = response?.ApiCallDetails
			?? seenExceptions?.LastOrDefault(e => e.Response?.ApiCallDetails != null)?.Response?.ApiCallDetails;
		return callDetails;
	}

	// ReSharper disable once ParameterOnlyUsedForPreconditionCheck.Local
	private void HandleTransportException(BoundConfiguration boundConfiguration, Exception clientException, TransportResponse response)
	{
		if (response.ApiCallDetails is ApiCallDetails a)
		{
			//if original exception was not explicitly set during the pipeline
			//set it to the TransportException we created for the bad response
			if (clientException != null && a.OriginalException == null)
				a.OriginalException = clientException;
			//On .NET Core the TransportClient implementation throws exceptions on bad responses
			//This causes it to behave differently to .NET FULL. We already wrapped the WebException
			//under TransportException, and it exposes way more information as part of its
			//exception message e.g. the root cause of the server error body.
#if NETFRAMEWORK
			if (a.OriginalException is WebException)
				a.OriginalException = clientException;
#endif
		}

		Configuration.OnRequestCompleted?.Invoke(response.ApiCallDetails);
		if (boundConfiguration != null && clientException != null && boundConfiguration.ThrowExceptions) throw clientException;
	}

	private void Ping(RequestPipeline pipeline, Node node, Auditor? auditor)
	{
		try
		{
			pipeline.Ping(node, auditor);
		}
		catch (PipelineException e) when (e.Recoverable)
		{
			if (_productRegistration.SupportsSniff)
				pipeline.SniffOnConnectionFailure(auditor);
			throw;
		}
	}

	private async Task PingAsync(RequestPipeline pipeline, Node node, Auditor? auditor, CancellationToken cancellationToken)
	{
		try
		{
			await pipeline.PingAsync(node, auditor, cancellationToken).ConfigureAwait(false);
		}
		catch (PipelineException e) when (e.Recoverable)
		{
			if (_productRegistration.SupportsSniff)
				await pipeline.SniffOnConnectionFailureAsync(auditor, cancellationToken).ConfigureAwait(false);
			throw;
		}
	}
}
