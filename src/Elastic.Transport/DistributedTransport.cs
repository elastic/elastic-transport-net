// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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

/// <inheritdoc cref="ITransport{TConfiguration}" />
public sealed class DistributedTransport : DistributedTransport<ITransportConfiguration>
{
	/// <summary>
	/// Transport coordinates the client requests over the node pool nodes and is in charge of falling over on
	/// different nodes
	/// </summary>
	/// <param name="configuration">The configuration to use for this transport</param>
	public DistributedTransport(ITransportConfiguration configuration)
		: base(configuration) { }
}

/// <inheritdoc cref="ITransport{TConfiguration}" />
public class DistributedTransport<TConfiguration> : ITransport<TConfiguration>
	where TConfiguration : class, ITransportConfiguration
{
	private readonly ProductRegistration _productRegistration;

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
		MemoryStreamFactory = configuration.MemoryStreamFactory;
		TransportRequestData = new RequestData(Configuration);
		TransportPipeline = Configuration.PipelineProvider.Create(TransportRequestData);
	}

	private RequestPipeline TransportPipeline { get; }
	private MemoryStreamFactory MemoryStreamFactory { get; }
	private RequestData TransportRequestData { get; }

	/// <inheritdoc cref="ITransport{TConfiguration}.Configuration"/>
	public TConfiguration Configuration { get; }

	/// <inheritdoc cref="ITransport.Request{TResponse}"/>
	public TResponse Request<TResponse>(
		in EndpointPath path,
		PostData? data,
		in OpenTelemetryData openTelemetryData,
		IRequestConfiguration? localConfiguration
	) where TResponse : TransportResponse, new() =>
		RequestCoreAsync<TResponse>(isAsync: false, path, data, openTelemetryData, localConfiguration)
			.EnsureCompleted();

	/// <inheritdoc cref="ITransport.RequestAsync{TResponse}"/>
	public Task<TResponse> RequestAsync<TResponse>(
		in EndpointPath path,
		PostData? data,
		in OpenTelemetryData openTelemetryData,
		IRequestConfiguration? localConfiguration,
		CancellationToken cancellationToken = default
	) where TResponse : TransportResponse, new() =>
		RequestCoreAsync<TResponse>(isAsync: true, path, data, openTelemetryData, localConfiguration, cancellationToken)
			.AsTask();

	private async ValueTask<TResponse> RequestCoreAsync<TResponse>(
		bool isAsync,
		EndpointPath path,
		PostData? data,
		OpenTelemetryData openTelemetryData,
		IRequestConfiguration? localConfiguration,
		CancellationToken cancellationToken = default
	) where TResponse : TransportResponse, new()
	{
		Activity activity = null;

		if (OpenTelemetry.ElasticTransportActivitySource.HasListeners())
			activity = OpenTelemetry.ElasticTransportActivitySource.StartActivity(openTelemetryData.SpanName ?? path.Method.GetStringValue(),
				ActivityKind.Client);

		try
		{
			//unless per request configuration or custom response builder is provided we can reuse a request data
			//that is specific to this transport
			var requestData =
				localConfiguration != null
					? new RequestData(Configuration, localConfiguration)
					: TransportRequestData;

			Configuration.OnRequestDataCreated?.Invoke(requestData);

			var pipeline = requestData == TransportRequestData ? TransportPipeline : Configuration.PipelineProvider.Create(requestData);
			var startedOn = Configuration.DateTimeProvider.Now();
			var auditor = Configuration.DisableAuditTrail.GetValueOrDefault(false) ? null : new Auditor(Configuration.DateTimeProvider);

			if (isAsync)
				await pipeline.FirstPoolUsageAsync(Configuration.BootstrapLock, auditor, cancellationToken).ConfigureAwait(false);
			else
				pipeline.FirstPoolUsage(Configuration.BootstrapLock, auditor);

			TResponse response = null;

			var endpoint = Endpoint.Empty(path);

			if (activity is { IsAllDataRequested: true })
			{
				if (activity.IsAllDataRequested)
					OpenTelemetry.SetCommonAttributes(activity, openTelemetryData, Configuration);

				if (Configuration.Authentication is BasicAuthentication basicAuthentication)
					activity.SetTag(SemanticConventions.DbUser, basicAuthentication.Username);

				activity.SetTag(OpenTelemetryAttributes.ElasticTransportProductName, Configuration.ProductRegistration.Name);
				activity.SetTag(OpenTelemetryAttributes.ElasticTransportProductVersion, Configuration.ProductRegistration.ProductAssemblyVersion);
				activity.SetTag(OpenTelemetryAttributes.ElasticTransportVersion, ReflectionVersionInfo.TransportVersion);
				activity.SetTag(SemanticConventions.UserAgentOriginal, Configuration.UserAgent.ToString());

				if (openTelemetryData.SpanAttributes is not null)
					foreach (var attribute in openTelemetryData.SpanAttributes)
						activity.SetTag(attribute.Key, attribute.Value);

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

				try
				{
					if (isAsync)
						response = await pipeline.CallProductEndpointAsync<TResponse>(endpoint, requestData, data, auditor, cancellationToken)
							.ConfigureAwait(false);
					else
						response = pipeline.CallProductEndpoint<TResponse>(endpoint, requestData, data, auditor);
				}
				catch (PipelineException pipelineException) when (!pipelineException.Recoverable)
				{
					HandlePipelineException(ref response, pipelineException, pipeline, singleNode, ref seenExceptions);
				}
				catch (PipelineException pipelineException)
				{
					HandlePipelineException(ref response, pipelineException, pipeline, singleNode, ref seenExceptions);
				}
				catch (Exception killerException)
				{
					ThrowUnexpectedTransportException(killerException, seenExceptions, endpoint, response, auditor);
				}
			}
			else
			{
				foreach (var node in pipeline.NextNode(startedOn, auditor))
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
							response = await pipeline.CallProductEndpointAsync<TResponse>(endpoint, requestData, data, auditor, cancellationToken)
								.ConfigureAwait(false);
						else
							response = pipeline.CallProductEndpoint<TResponse>(endpoint, requestData, data, auditor);

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

#if NET6_0_OR_GREATER
			activity?.SetStatus(response.ApiCallDetails.HasSuccessfulStatusCodeAndExpectedContentType ? ActivityStatusCode.Ok : ActivityStatusCode.Error);
#endif

			activity?.SetTag(SemanticConventions.HttpResponseStatusCode, response.ApiCallDetails.HttpStatusCode);
			activity?.SetTag(OpenTelemetryAttributes.ElasticTransportAttemptedNodes, attemptedNodes);

			return FinalizeResponse(endpoint, requestData, data, pipeline, startedOn, auditor, seenExceptions, response);
		}
		finally
		{
			activity?.Dispose();
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

	private TResponse FinalizeResponse<TResponse>(
		Endpoint endpoint,
		RequestData requestData,
		PostData? postData,
		RequestPipeline pipeline,
		DateTimeOffset startedOn,
		Auditor auditor,
		List<PipelineException>? seenExceptions,
		TResponse? response
	) where TResponse : TransportResponse, new()
	{
		if (endpoint.IsEmpty) //foreach never ran
			pipeline.ThrowNoNodesAttempted(endpoint, auditor, seenExceptions);

		var callDetails = GetMostRecentCallDetails(response, seenExceptions);
		var clientException = pipeline.CreateClientException(response, callDetails, endpoint, auditor, startedOn, seenExceptions);

		if (response?.ApiCallDetails == null)
			pipeline.BadResponse(ref response, callDetails, endpoint, requestData, postData, clientException, auditor);

		HandleTransportException(requestData, clientException, response);
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
	private void HandleTransportException(RequestData data, Exception clientException, TransportResponse response)
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
		if (data != null && clientException != null && data.ThrowExceptions) throw clientException;
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
