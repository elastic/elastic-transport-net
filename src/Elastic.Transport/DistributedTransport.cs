// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Elastic.Transport.Diagnostics;
using Elastic.Transport.Extensions;
using Elastic.Transport.Products;

#if NETFRAMEWORK
using System.Net;
#endif

namespace Elastic.Transport;

/// <inheritdoc cref="ITransport{TConfiguration}" />
public sealed class DistributedTransport : DistributedTransport<TransportConfiguration>
{
	/// <summary>
	///     Transport coordinates the client requests over the node pool nodes and is in charge of falling over on
	///     different
	///     nodes
	/// </summary>
	/// <param name="configurationValues">The configuration to use for this transport</param>
	public DistributedTransport(TransportConfiguration configurationValues) : base(configurationValues, null, null) { }

	/// <summary>
	///     Transport coordinates the client requests over the node pool nodes and is in charge of falling over on
	///     different
	///     nodes
	/// </summary>
	/// <param name="configurationValues">The configuration to use for this transport</param>
	/// <param name="pipelineProvider">In charge of create a new pipeline, safe to pass null to use the default</param>
	/// <param name="dateTimeProvider">The date time proved to use, safe to pass null to use the default</param>
	/// <param name="memoryStreamFactory">The memory stream provider to use, safe to pass null to use the default</param>
	internal DistributedTransport(
		TransportConfiguration configurationValues,
		RequestPipelineFactory<TransportConfiguration>? pipelineProvider = null,
		DateTimeProvider? dateTimeProvider = null,
		MemoryStreamFactory? memoryStreamFactory = null
	)
		: base(configurationValues, pipelineProvider, dateTimeProvider, memoryStreamFactory) { }
}

/// <inheritdoc cref="ITransport{TConfiguration}" />
public class DistributedTransport<TConfiguration> : ITransport<TConfiguration>
	where TConfiguration : class, ITransportConfiguration
{
	private readonly ProductRegistration _productRegistration;

	/// <summary>
	///     Transport coordinates the client requests over the node pool nodes and is in charge of falling over on
	///     different
	///     nodes
	/// </summary>
	/// <param name="configurationValues">The configuration to use for this transport</param>
	/// <param name="pipelineProvider">In charge of create a new pipeline, safe to pass null to use the default</param>
	/// <param name="dateTimeProvider">The date time proved to use, safe to pass null to use the default</param>
	/// <param name="memoryStreamFactory">The memory stream provider to use, safe to pass null to use the default</param>
	public DistributedTransport(
		TConfiguration configurationValues,
		RequestPipelineFactory<TConfiguration>? pipelineProvider = null,
		DateTimeProvider? dateTimeProvider = null,
		MemoryStreamFactory? memoryStreamFactory = null
	)
	{
		configurationValues.ThrowIfNull(nameof(configurationValues));
		configurationValues.NodePool.ThrowIfNull(nameof(configurationValues.NodePool));
		configurationValues.Connection.ThrowIfNull(nameof(configurationValues.Connection));
		configurationValues.RequestResponseSerializer.ThrowIfNull(nameof(configurationValues
			.RequestResponseSerializer));

		_productRegistration = configurationValues.ProductRegistration;
		Configuration = configurationValues;
		PipelineProvider = pipelineProvider ?? new DefaultRequestPipelineFactory<TConfiguration>();
		DateTimeProvider = dateTimeProvider ?? DefaultDateTimeProvider.Default;
		MemoryStreamFactory = memoryStreamFactory ?? configurationValues.MemoryStreamFactory;
	}

	private DateTimeProvider DateTimeProvider { get; }
	private MemoryStreamFactory MemoryStreamFactory { get; }
	private RequestPipelineFactory<TConfiguration> PipelineProvider { get; }

	/// <inheritdoc cref="ITransport{TConfiguration}.Configuration"/>
	public TConfiguration Configuration { get; }

	/// <inheritdoc cref="ITransport.Request{TResponse}"/>
	public TResponse Request<TResponse>(
		in EndpointPath path,
		PostData? data,
		in OpenTelemetryData openTelemetryData,
		IRequestConfiguration? localConfiguration,
		CustomResponseBuilder? responseBuilder
	)
		where TResponse : TransportResponse, new() =>
		RequestCoreAsync<TResponse>(isAsync: false, path, data, openTelemetryData, localConfiguration, responseBuilder)
			.EnsureCompleted();

	/// <inheritdoc cref="ITransport.RequestAsync{TResponse}"/>
	public Task<TResponse> RequestAsync<TResponse>(
		in EndpointPath path,
		PostData? data,
		in OpenTelemetryData openTelemetryData,
		IRequestConfiguration? localConfiguration,
		CustomResponseBuilder? responseBuilder,
		CancellationToken cancellationToken = default
	)
		where TResponse : TransportResponse, new() =>
		RequestCoreAsync<TResponse>(isAsync: true, path, data, openTelemetryData, localConfiguration, responseBuilder, cancellationToken)
			.AsTask();

	private async ValueTask<TResponse> RequestCoreAsync<TResponse>(
		bool isAsync,
		EndpointPath path,
		PostData? data,
		OpenTelemetryData openTelemetryData,
		IRequestConfiguration? localRequestConfiguration,
		CustomResponseBuilder? customResponseBuilder,
		CancellationToken cancellationToken = default
	)
		where TResponse : TransportResponse, new()
	{
		Activity activity = null;

		if (OpenTelemetry.ElasticTransportActivitySource.HasListeners())
			activity = OpenTelemetry.ElasticTransportActivitySource.StartActivity(openTelemetryData.SpanName ?? path.Method.GetStringValue(),
				ActivityKind.Client);

		try
		{
			using var pipeline = PipelineProvider.Create(Configuration, DateTimeProvider, MemoryStreamFactory, localRequestConfiguration);

			if (isAsync)
				await pipeline.FirstPoolUsageAsync(Configuration.BootstrapLock, cancellationToken).ConfigureAwait(false);
			else
				pipeline.FirstPoolUsage(Configuration.BootstrapLock);

			//var pathAndQuery = requestParameters?.CreatePathWithQueryStrings(path, Configuration) ?? path;
			var requestData = new RequestData(Configuration, localRequestConfiguration, customResponseBuilder, MemoryStreamFactory);
			Configuration.OnRequestDataCreated?.Invoke(requestData);
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
						response = await pipeline.CallProductEndpointAsync<TResponse>(endpoint, requestData, data, cancellationToken)
							.ConfigureAwait(false);
					else
						response = pipeline.CallProductEndpoint<TResponse>(endpoint, requestData, data);
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
					ThrowUnexpectedTransportException(killerException, seenExceptions, endpoint, response, pipeline);
				}
			}
			else
			{
				foreach (var node in pipeline.NextNode())
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
								await pipeline.SniffOnStaleClusterAsync(cancellationToken).ConfigureAwait(false);
							else
								pipeline.SniffOnStaleCluster();
						}
						if (_productRegistration.SupportsPing)
						{
							if (isAsync)
								await PingAsync(pipeline, node, cancellationToken).ConfigureAwait(false);
							else
								Ping(pipeline, node);
						}

						if (isAsync)
							response = await pipeline.CallProductEndpointAsync<TResponse>(endpoint, requestData, data, cancellationToken)
								.ConfigureAwait(false);
						else
							response = pipeline.CallProductEndpoint<TResponse>(endpoint, requestData, data);

						if (!response.ApiCallDetails.SuccessOrKnownError)
						{
							pipeline.MarkDead(node);

							if (_productRegistration.SupportsSniff)
							{
								if (isAsync)
									await pipeline.SniffOnConnectionFailureAsync(cancellationToken).ConfigureAwait(false);
								else
									pipeline.SniffOnConnectionFailure();
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
							pipeline.AuditCancellationRequested();

						throw new UnexpectedTransportException(killerException, seenExceptions)
						{
							Endpoint = endpoint,
							ApiCallDetails = response?.ApiCallDetails,
							AuditTrail = pipeline.AuditTrail
						};
					}

					if (cancellationToken.IsCancellationRequested)
					{
						pipeline.AuditCancellationRequested();
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

			return FinalizeResponse(endpoint, requestData, data, pipeline, seenExceptions, response);
		}
		finally
		{
			activity?.Dispose();
		}
	}

	private static void ThrowUnexpectedTransportException<TResponse>(Exception killerException,
		List<PipelineException> seenExceptions,
		Endpoint endpoint,
		TResponse response, RequestPipeline pipeline
	) where TResponse : TransportResponse, new() =>
		throw new UnexpectedTransportException(killerException, seenExceptions)
		{
			Endpoint = endpoint, ApiCallDetails = response?.ApiCallDetails, AuditTrail = pipeline.AuditTrail
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

	private TResponse FinalizeResponse<TResponse>(Endpoint endpoint, RequestData requestData, PostData? postData, RequestPipeline pipeline,
		List<PipelineException>? seenExceptions,
		TResponse? response
	) where TResponse : TransportResponse, new()
	{
		if (endpoint.IsEmpty) //foreach never ran
			pipeline.ThrowNoNodesAttempted(endpoint, seenExceptions);

		var callDetails = GetMostRecentCallDetails(response, seenExceptions);
		var clientException = pipeline.CreateClientException(response, callDetails, endpoint, requestData, seenExceptions);

		if (response?.ApiCallDetails == null)
			pipeline.BadResponse(ref response, callDetails, endpoint, requestData, postData, clientException);

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
			//under TransportException and it exposes way more information as part of it's
			//exception message e.g the the root cause of the server error body.
#if NETFRAMEWORK
			if (a.OriginalException is WebException)
				a.OriginalException = clientException;
#endif
		}

		Configuration.OnRequestCompleted?.Invoke(response.ApiCallDetails);
		if (data != null && clientException != null && data.ThrowExceptions) throw clientException;
	}

	private void Ping(RequestPipeline pipeline, Node node)
	{
		try
		{
			pipeline.Ping(node);
		}
		catch (PipelineException e) when (e.Recoverable)
		{
			if (_productRegistration.SupportsSniff)
				pipeline.SniffOnConnectionFailure();
			throw;
		}
	}

	private async Task PingAsync(RequestPipeline pipeline, Node node, CancellationToken cancellationToken)
	{
		try
		{
			await pipeline.PingAsync(node, cancellationToken).ConfigureAwait(false);
		}
		catch (PipelineException e) when (e.Recoverable)
		{
			if (_productRegistration.SupportsSniff)
				await pipeline.SniffOnConnectionFailureAsync(cancellationToken).ConfigureAwait(false);
			throw;
		}
	}
}
