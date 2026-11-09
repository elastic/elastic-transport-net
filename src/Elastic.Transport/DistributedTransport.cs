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
#if NET8_0_OR_GREATER
using System.Net.Http;
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

	// A bound configuration and the pipeline that runs on it. A pipeline is a pure function of its bound configuration,
	// so the two are built once per distinct per-request configuration and cached together.
	private sealed record Bound(BoundConfiguration Configuration, RequestPipeline Pipeline);

	private ConditionalWeakTable<RequestConfiguration, Bound>? _boundConfigurations;

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
		var transportBoundConfiguration = new BoundConfiguration(Configuration);
		_transportBound = new Bound(transportBoundConfiguration, CreatePipeline(transportBoundConfiguration));
		_createBound = rc =>
		{
			var bound = new BoundConfiguration(Configuration, rc);
			return new Bound(bound, CreatePipeline(bound));
		};
	}

	private readonly Bound _transportBound;
	private readonly ConditionalWeakTable<RequestConfiguration, Bound>.CreateValueCallback _createBound;

	private RequestPipeline CreatePipeline(BoundConfiguration boundConfiguration) =>
		((Configuration as IHasPipelineProvider)?.PipelineProvider ?? DefaultRequestPipelineFactory.Default).Create(boundConfiguration);

	/// <inheritdoc cref="ITransport{TConfiguration}.Configuration"/>
	public TConfiguration Configuration { get; }

	ITransportConfiguration ITransport.Configuration => Configuration;

	/// <inheritdoc cref="ITransport.Request{TResponse}"/>
	public TResponse Request<TResponse>(
		in EndpointPath path,
		PostData? postData,
		Action<Activity>? configureActivity,
		IRequestConfiguration? localConfiguration
	) where TResponse : TransportResponse, new() =>
		RequestCoreAsync<TResponse>(isAsync: false, path, postData, configureActivity, localConfiguration)
			.EnsureCompleted();

	/// <inheritdoc cref="ITransport.RequestAsync{TResponse}"/>
	public Task<TResponse> RequestAsync<TResponse>(
		in EndpointPath path,
		PostData? postData,
		Action<Activity>? configureActivity,
		IRequestConfiguration? localConfiguration,
		CancellationToken cancellationToken = default
	) where TResponse : TransportResponse, new() =>
		RequestCoreAsync<TResponse>(isAsync: true, path, postData, configureActivity, localConfiguration, cancellationToken)
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
		Activity? activity = null;

		if (OpenTelemetry.ElasticTransportActivitySource.HasListeners())
			activity = OpenTelemetry.ElasticTransportActivitySource.StartActivity(path.Method.GetStringValue(),
				ActivityKind.Client);

		try
		{
			var (boundConfiguration, pipeline) = BindConfiguration(localConfiguration);

			Configuration.OnConfigurationBound?.Invoke(boundConfiguration);

			var dateTimeProvider = Configuration.DateTimeProvider ?? DefaultDateTimeProvider.Default;
			var startedOn = dateTimeProvider.Now();
			var auditor = boundConfiguration.DisableAuditTrail ? null : new Auditor(dateTimeProvider);

			if (isAsync)
				await pipeline.FirstPoolUsageAsync(Configuration.BootstrapLock, auditor, cancellationToken).ConfigureAwait(false);
			else
				pipeline.FirstPoolUsage(Configuration.BootstrapLock, auditor);

			TResponse? response = null;

			var endpoint = Endpoint.Empty(path);

			if (activity is { IsAllDataRequested: true })
			{
				if (Configuration.Authentication is BasicAuthentication basicAuthentication)
					activity.SetTag(SemanticConventions.DbUser, basicAuthentication.Username);

				activity.SetTag(OpenTelemetryAttributes.ElasticTransportProductName, Configuration.ProductRegistration.Name);
				activity.SetTag(OpenTelemetryAttributes.ElasticTransportProductVersion, Configuration.ProductRegistration.ProductAssemblyVersion);
				activity.SetTag(OpenTelemetryAttributes.ElasticTransportVersion, ReflectionVersionInfo.TransportVersion);
				activity.SetTag(SemanticConventions.UserAgentOriginal, Configuration.UserAgent?.ToString());
				activity.SetTag(SemanticConventions.HttpRequestMethod, endpoint.Method.GetStringValue());
			}

			// A single-node request needs no per-request bookkeeping unless something fails, so it does not allocate
			// a RequestAttempt up front. Walking several nodes always does.
			RequestAttempt? attempt = null;
			var attemptedNodes = 0;

			if (pipeline.TryGetSingleNode(out var singleNode) && singleNode is not null)
			{
				endpoint = endpoint with { Node = singleNode };
				// No value in marking a single node as dead. We have no other options!
				attemptedNodes = 1;
				SetServerTags(activity, endpoint);

				// Nothing to fail over to, ping or sniff. The only second chance is a pooled connection that a
				// server or load balancer closed behind our back.
				for (var retry = 0; retry < 2; retry++)
				{
					try
					{
						if (isAsync)
							response = await pipeline.CallProductEndpointAsync<TResponse>(endpoint, boundConfiguration, data, auditor, cancellationToken)
								.ConfigureAwait(false);
						else
							response = pipeline.CallProductEndpoint<TResponse>(endpoint, boundConfiguration, data, auditor);
					}
					catch (PipelineException pipelineException)
					{
						attempt ??= new RequestAttempt { AttemptedNodes = attemptedNodes };
						HandlePipelineException(ref response, pipelineException, pipeline, singleNode, attempt);
						break;
					}
					catch (Exception killerException)
					{
						ThrowUnexpectedTransportException(killerException, attempt?.SeenExceptions, endpoint, response, auditor);
					}

					// Retry once when a pooled connection was closed by the server/LB
					// before we used it. SocketsHttpHandler classifies this as InvalidResponse
					// rather than a connection error, so it does not auto-retry internally.
					// The stale connection is purged after the first failure; the retry
					// succeeds on a fresh connection.
					if (retry == 0 && IsStaleConnectionException(response))
					{
						attemptedNodes++;
						continue;
					}

					break;
				}
			}
			else
			{
				attempt = new RequestAttempt();

				foreach (var node in pipeline.NextNode(startedOn, attempt, auditor))
				{
					attempt.AttemptedNodes++;
					endpoint = endpoint with { Node = node };

					// If multiple nodes are attempted, the final node attempted will be used to set the operation span attributes.
					// Each physical node attempt in CallProductEndpoint will also record these attributes.
					SetServerTags(activity, endpoint);

					// One node's turn: keep the topology fresh, make sure the node is up, call it, then Settle on what
					// that means. The awaits stay here, in the one state machine, on purpose: moving them into a helper
					// method added a state machine box (about 200 B) to every request that really does I/O.
					NodeOutcome outcome;
					try
					{
						if (_productRegistration.SupportsSniff)
						{
							if (isAsync)
								await pipeline.SniffOnStaleClusterAsync(auditor, attempt, cancellationToken).ConfigureAwait(false);
							else
								pipeline.SniffOnStaleCluster(auditor, attempt);
						}
						if (_productRegistration.SupportsPing)
						{
							if (isAsync)
								await PingAsync(pipeline, node, auditor, attempt, cancellationToken).ConfigureAwait(false);
							else
								Ping(pipeline, node, auditor, attempt);
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
									await pipeline.SniffOnConnectionFailureAsync(auditor, attempt, cancellationToken).ConfigureAwait(false);
								else
									pipeline.SniffOnConnectionFailure(auditor, attempt);
							}
						}

						outcome = Settle(pipeline, node, response, auditor, cancellationToken);
					}
					catch (PipelineException pipelineException) when (!pipelineException.Recoverable)
					{
						HandlePipelineException(ref response, pipelineException, pipeline, node, attempt);
						break;
					}
					catch (PipelineException pipelineException)
					{
						HandlePipelineException(ref response, pipelineException, pipeline, node, attempt);
						outcome = Settle(pipeline, node, response, auditor, cancellationToken);
					}
					catch (Exception killerException)
					{
						if (killerException is OperationCanceledException && cancellationToken.IsCancellationRequested)
							pipeline.AuditCancellationRequested(auditor);

						throw new UnexpectedTransportException(killerException, attempt.SeenExceptions)
						{
							Endpoint = endpoint,
							ApiCallDetails = response?.ApiCallDetails,
							AuditTrail = auditor
						};
					}

					if (outcome != NodeOutcome.TryNextNode)
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
			activity?.SetTag(OpenTelemetryAttributes.ElasticTransportAttemptedNodes, attempt?.AttemptedNodes ?? attemptedNodes);

			// We don't check IsAllDataRequested here as that's left to the consumer.
			if (configureActivity is not null && activity is not null)
				configureActivity.Invoke(activity);

			if (activity is { IsAllDataRequested: true })
				OpenTelemetry.SetCommonAttributes(activity, Configuration);

			return FinalizeResponse(endpoint, boundConfiguration, data, pipeline, startedOn, attempt?.AttemptedNodes ?? attemptedNodes, auditor, attempt?.SeenExceptions, response);
		}
		finally
		{
			activity?.Dispose();
		}
	}

	/// <summary>
	/// What happened to a request on one node, and so what the walk over the nodes does next. This is the whole of the
	/// "per node" part of the request diagram: everything else is the order in which nodes are offered.
	/// </summary>
	private enum NodeOutcome
	{
		/// <summary>The node gave a usable answer: it is marked alive and the request is done.</summary>
		Succeeded,

		/// <summary>The node failed in a way another node may not: it is marked dead and the next node is tried.</summary>
		TryNextNode,

		/// <summary>The request cannot continue, whatever the other nodes might say (bad credentials, cancellation).</summary>
		Stop,
	}

	private static void SetServerTags(Activity? activity, Endpoint endpoint)
	{
		if (activity is null)
			return;

		activity.SetTag(SemanticConventions.UrlFull, endpoint.Uri?.AbsoluteUri);
		activity.SetTag(SemanticConventions.ServerAddress, endpoint.Uri?.Host);
		activity.SetTag(SemanticConventions.ServerPort, endpoint.Uri?.Port);
	}

	/// <summary>
	/// Decides what one node's turn means for the walk over the nodes. Pure apart from marking the node alive and
	/// auditing a cancellation, and deliberately not async: see the note in <see cref="RequestCoreAsync{TResponse}"/>.
	/// </summary>
	private static NodeOutcome Settle<TResponse>(RequestPipeline pipeline, Node node, TResponse? response, Auditor? auditor, CancellationToken cancellationToken)
		where TResponse : TransportResponse, new()
	{
		if (cancellationToken.IsCancellationRequested)
		{
			pipeline.AuditCancellationRequested(auditor);
			return NodeOutcome.Stop;
		}

		if (response == null || !response.ApiCallDetails.SuccessOrKnownError)
			return NodeOutcome.TryNextNode;

		pipeline.MarkAlive(node);
		return NodeOutcome.Succeeded;
	}

	private (BoundConfiguration Configuration, RequestPipeline Pipeline) BindConfiguration(IRequestConfiguration? localConfiguration)
	{
		// Unless per request configuration is provided, we can reuse the BoundConfiguration and pipeline
		// that are specific to this transport.
		switch (localConfiguration)
		{
			case null:
				return (_transportBound.Configuration, _transportBound.Pipeline);
			case RequestConfiguration rc:
				// Since `IRequestConfiguration` might be implemented as mutable class, we only cache for the
				// immutable `RequestConfiguration` record.
				var cache = _boundConfigurations ?? InitializeCache();
				var bound = cache.GetValue(rc, _createBound);
				return (bound.Configuration, bound.Pipeline);
			default:
				var bindWithoutCache = new BoundConfiguration(Configuration, localConfiguration);
				return (bindWithoutCache, CreatePipeline(bindWithoutCache));
		}
	}

	// Only allocated once a request actually carries its own configuration; creating the table eagerly as an argument
	// to Interlocked.CompareExchange built (and threw away) a new one on every such request.
	private ConditionalWeakTable<RequestConfiguration, Bound> InitializeCache() =>
		Interlocked.CompareExchange(ref _boundConfigurations, new ConditionalWeakTable<RequestConfiguration, Bound>(), null)
		?? _boundConfigurations!;

	private static void ThrowUnexpectedTransportException<TResponse>(Exception killerException,
		List<PipelineException>? seenExceptions,
		Endpoint endpoint,
		TResponse? response, IReadOnlyCollection<Audit>? auditTrail
	) where TResponse : TransportResponse, new() =>
		throw new UnexpectedTransportException(killerException, seenExceptions)
		{
			Endpoint = endpoint,
			ApiCallDetails = response?.ApiCallDetails,
			AuditTrail = auditTrail
		};

	private static void HandlePipelineException<TResponse>(
		ref TResponse? response, PipelineException ex, RequestPipeline pipeline, Node node, RequestAttempt attempt
	)
		where TResponse : TransportResponse, new()
	{
		response ??= ex.Response as TResponse;
		pipeline.MarkDead(node);
		(attempt.SeenExceptions ??= new List<PipelineException>(1)).Add(ex);
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
		#if NET8_0_OR_GREATER
			if (ex is HttpRequestException { HttpRequestError: HttpRequestError.InvalidResponse })
				return true;
		#endif
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
		Auditor? auditor,
		List<PipelineException>? seenExceptions,
		TResponse? response
	) where TResponse : TransportResponse, new()
	{
		if (endpoint.IsEmpty) //foreach never ran
			pipeline.ThrowNoNodesAttempted(endpoint, auditor, seenExceptions);

		var callDetails = GetMostRecentCallDetails(response, seenExceptions);
		var clientException = pipeline.CreateClientException(response, callDetails, endpoint, auditor, startedOn, attemptedNodes, seenExceptions);

		if (response?.ApiCallDetails == null)
			pipeline.BadResponse(ref response, callDetails, endpoint, boundConfiguration, postData, clientException!, auditor);

		HandleTransportException(boundConfiguration, clientException, response!);
		return response!;
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
	private void HandleTransportException(BoundConfiguration boundConfiguration, Exception? clientException, TransportResponse response)
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
		if (clientException != null && boundConfiguration.ThrowExceptions)
			throw clientException;
	}

	private void Ping(RequestPipeline pipeline, Node node, Auditor? auditor, RequestAttempt attempt)
	{
		try
		{
			pipeline.Ping(node, auditor);
		}
		catch (PipelineException e) when (e.Recoverable)
		{
			if (_productRegistration.SupportsSniff)
				pipeline.SniffOnConnectionFailure(auditor, attempt);
			throw;
		}
	}

	private async Task PingAsync(RequestPipeline pipeline, Node node, Auditor? auditor, RequestAttempt attempt, CancellationToken cancellationToken)
	{
		try
		{
			await pipeline.PingAsync(node, auditor, cancellationToken).ConfigureAwait(false);
		}
		catch (PipelineException e) when (e.Recoverable)
		{
			if (_productRegistration.SupportsSniff)
				await pipeline.SniffOnConnectionFailureAsync(auditor, attempt, cancellationToken).ConfigureAwait(false);
			throw;
		}
	}
}
