// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Elastic.Transport.Diagnostics;
using Elastic.Transport.Extensions;
using Elastic.Transport.Products;

#if NETFRAMEWORK
using System.Net;
#endif

namespace Elastic.Transport;

/// <inheritdoc cref="HttpTransport{TConnectionSettings}" />
public sealed class DefaultHttpTransport : DefaultHttpTransport<TransportConfiguration>
{
	/// <summary>
	///     Transport coordinates the client requests over the node pool nodes and is in charge of falling over on
	///     different
	///     nodes
	/// </summary>
	/// <param name="configurationValues">The connection settings to use for this transport</param>
	public DefaultHttpTransport(TransportConfiguration configurationValues) : base(configurationValues)
	{
	}

	/// <summary>
	///     Transport coordinates the client requests over the node pool nodes and is in charge of falling over on
	///     different
	///     nodes
	/// </summary>
	/// <param name="configurationValues">The connection settings to use for this transport</param>
	/// <param name="dateTimeProvider">The date time proved to use, safe to pass null to use the default</param>
	/// <param name="memoryStreamFactory">The memory stream provider to use, safe to pass null to use the default</param>
	public DefaultHttpTransport(TransportConfiguration configurationValues,
		DateTimeProvider dateTimeProvider = null, MemoryStreamFactory memoryStreamFactory = null
	)
		: base(configurationValues, null, dateTimeProvider, memoryStreamFactory)
	{
	}

	/// <summary>
	///     Transport coordinates the client requests over the node pool nodes and is in charge of falling over on
	///     different
	///     nodes
	/// </summary>
	/// <param name="configurationValues">The connection settings to use for this transport</param>
	/// <param name="pipelineProvider">In charge of create a new pipeline, safe to pass null to use the default</param>
	/// <param name="dateTimeProvider">The date time proved to use, safe to pass null to use the default</param>
	/// <param name="memoryStreamFactory">The memory stream provider to use, safe to pass null to use the default</param>
	internal DefaultHttpTransport(TransportConfiguration configurationValues,
		RequestPipelineFactory<TransportConfiguration> pipelineProvider = null,
		DateTimeProvider dateTimeProvider = null, MemoryStreamFactory memoryStreamFactory = null
	)
		: base(configurationValues, pipelineProvider, dateTimeProvider, memoryStreamFactory)
	{
	}
}

/// <inheritdoc cref="HttpTransport{TConfiguration}" />
public class DefaultHttpTransport<TConfiguration> : HttpTransport<TConfiguration>
	where TConfiguration : class, ITransportConfiguration
{
	private readonly ProductRegistration _productRegistration;

	/// <summary>
	///     Transport coordinates the client requests over the node pool nodes and is in charge of falling over on
	///     different
	///     nodes
	/// </summary>
	/// <param name="configurationValues">The connection settings to use for this transport</param>
	public DefaultHttpTransport(TConfiguration configurationValues) : this(configurationValues, null, null, null)
	{
	}

	/// <summary>
	///     Transport coordinates the client requests over the node pool nodes and is in charge of falling over on
	///     different
	///     nodes
	/// </summary>
	/// <param name="configurationValues">The connection settings to use for this transport</param>
	/// <param name="dateTimeProvider">The date time proved to use, safe to pass null to use the default</param>
	/// <param name="memoryStreamFactory">The memory stream provider to use, safe to pass null to use the default</param>
	public DefaultHttpTransport(
		TConfiguration configurationValues,
		DateTimeProvider dateTimeProvider = null,
		MemoryStreamFactory memoryStreamFactory = null)
			: this(configurationValues, null, dateTimeProvider, memoryStreamFactory) { }

	/// <summary>
	///     Transport coordinates the client requests over the node pool nodes and is in charge of falling over on
	///     different
	///     nodes
	/// </summary>
	/// <param name="configurationValues">The connection settings to use for this transport</param>
	/// <param name="pipelineProvider">In charge of create a new pipeline, safe to pass null to use the default</param>
	/// <param name="dateTimeProvider">The date time proved to use, safe to pass null to use the default</param>
	/// <param name="memoryStreamFactory">The memory stream provider to use, safe to pass null to use the default</param>
	public DefaultHttpTransport(
		TConfiguration configurationValues,
		RequestPipelineFactory<TConfiguration> pipelineProvider = null,
		DateTimeProvider dateTimeProvider = null,
		MemoryStreamFactory memoryStreamFactory = null
	)
	{
		configurationValues.ThrowIfNull(nameof(configurationValues));
		configurationValues.NodePool.ThrowIfNull(nameof(configurationValues.NodePool));
		configurationValues.Connection.ThrowIfNull(nameof(configurationValues.Connection));
		configurationValues.RequestResponseSerializer.ThrowIfNull(nameof(configurationValues
			.RequestResponseSerializer));

		_productRegistration = configurationValues.ProductRegistration;
		Settings = configurationValues;
		PipelineProvider = pipelineProvider ?? new DefaultRequestPipelineFactory<TConfiguration>();
		DateTimeProvider = dateTimeProvider ?? DefaultDateTimeProvider.Default;
		MemoryStreamFactory = memoryStreamFactory ?? configurationValues.MemoryStreamFactory;
	}

	private DateTimeProvider DateTimeProvider { get; }
	private MemoryStreamFactory MemoryStreamFactory { get; }
	private RequestPipelineFactory<TConfiguration> PipelineProvider { get; }

	/// <summary>
	///
	/// </summary>
	public override TConfiguration Settings { get; }

	/// <inheritdoc cref="HttpTransport.Request{TResponse}(HttpMethod, string, PostData?, RequestParameters?, OpenTelemetryData)"/>
	public override TResponse Request<TResponse>(
		HttpMethod method,
		string path,
		PostData? data,
		RequestParameters? requestParameters,
		OpenTelemetryData openTelemetryData)
			=> RequestCoreAsync<TResponse>(false, method, path, data, requestParameters, openTelemetryData).EnsureCompleted();

	/// <inheritdoc cref="HttpTransport.RequestAsync{TResponse}(HttpMethod, string, PostData?, RequestParameters?, OpenTelemetryData, CancellationToken)"/>
	public override Task<TResponse> RequestAsync<TResponse>(
		HttpMethod method,
		string path,
		PostData? data,
		RequestParameters? requestParameters,
		OpenTelemetryData openTelemetryData,
		CancellationToken cancellationToken = default)
			=> RequestCoreAsync<TResponse>(true, method, path, data, requestParameters, openTelemetryData, cancellationToken).AsTask();
	
	private async ValueTask<TResponse> RequestCoreAsync<TResponse>(
		bool isAsync,
		HttpMethod method,
		string path,
		PostData? data,
		RequestParameters? requestParameters,
		OpenTelemetryData openTelemetryData,
		CancellationToken cancellationToken = default)
			where TResponse : TransportResponse, new()
	{
		using var pipeline =
			PipelineProvider.Create(Settings, DateTimeProvider, MemoryStreamFactory, requestParameters);

		if (isAsync)
			await pipeline.FirstPoolUsageAsync(Settings.BootstrapLock, cancellationToken).ConfigureAwait(false);
		else
			pipeline.FirstPoolUsage(Settings.BootstrapLock);

		var requestData = new RequestData(method, path, data, Settings, requestParameters, MemoryStreamFactory);
		Settings.OnRequestDataCreated?.Invoke(requestData);
		TResponse response = null;

		List<PipelineException>? seenExceptions = null;

		if (pipeline.TryGetSingleNode(out var singleNode))
		{
			// No value in marking a single node as dead. We have no other options!

			requestData.Node = singleNode;

			try
			{
				if (isAsync)
					response = await pipeline.CallProductEndpointAsync<TResponse>(requestData, cancellationToken)
						.ConfigureAwait(false);
				else
					response = pipeline.CallProductEndpoint<TResponse>(requestData);
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
				ThrowUnexpectedTransportException(killerException, seenExceptions, requestData, response, pipeline);
			}
		}
		else
			foreach (var node in pipeline.NextNode())
			{
				requestData.Node = node;
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
						response = await pipeline.CallProductEndpointAsync<TResponse>(requestData, cancellationToken)
							.ConfigureAwait(false);
					else
						response = pipeline.CallProductEndpoint<TResponse>(requestData);

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
						Request = requestData,
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

		return FinalizeResponse(requestData, pipeline, seenExceptions, response);
	}

	private static void ThrowUnexpectedTransportException<TResponse>(Exception killerException,
		List<PipelineException> seenExceptions,
		RequestData requestData,
		TResponse response, RequestPipeline pipeline
	) where TResponse : TransportResponse, new() =>
		throw new UnexpectedTransportException(killerException, seenExceptions)
		{
			Request = requestData, ApiCallDetails = response?.ApiCallDetails, AuditTrail = pipeline.AuditTrail
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

	private TResponse FinalizeResponse<TResponse>(RequestData requestData, RequestPipeline pipeline,
		List<PipelineException>? seenExceptions,
		TResponse? response
	) where TResponse : TransportResponse, new()
	{
		if (requestData.Node == null) //foreach never ran
			pipeline.ThrowNoNodesAttempted(requestData, seenExceptions);

		var callDetails = GetMostRecentCallDetails(response, seenExceptions);
		var clientException = pipeline.CreateClientException(response, callDetails, requestData, seenExceptions);

		if (response?.ApiCallDetails == null)
			pipeline.BadResponse(ref response, callDetails, requestData, clientException);

		HandleTransportException(requestData, clientException, response);
		return response;
	}

	private static ApiCallDetails? GetMostRecentCallDetails<TResponse>(TResponse? response,
		IEnumerable<PipelineException>? seenExceptions)
		where TResponse : TransportResponse, new()
	{
		var callDetails = response?.ApiCallDetails ?? seenExceptions?.LastOrDefault(e => e.Response?.ApiCallDetails != null)?.Response?.ApiCallDetails;
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

		Settings.OnRequestCompleted?.Invoke(response.ApiCallDetails);
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
