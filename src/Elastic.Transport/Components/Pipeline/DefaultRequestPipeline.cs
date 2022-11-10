// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Elastic.Transport.Diagnostics;
using Elastic.Transport.Diagnostics.Auditing;
using Elastic.Transport.Extensions;
using Elastic.Transport.Products;
using static Elastic.Transport.Diagnostics.Auditing.AuditEvent;

//#if NETSTANDARD2_0 || NETSTANDARD2_1
//using System.Threading.Tasks.Extensions;
//#endif

namespace Elastic.Transport;

/// <inheritdoc cref="RequestPipeline" />
public class DefaultRequestPipeline<TConfiguration> : RequestPipeline
	where TConfiguration : class, ITransportConfiguration
{
	private readonly TransportClient _transportClient;
	private readonly NodePool _nodePool;
	private readonly DateTimeProvider _dateTimeProvider;
	private readonly MemoryStreamFactory _memoryStreamFactory;
	private readonly Func<Node, bool> _nodePredicate;
	private readonly ProductRegistration _productRegistration;
	private readonly TConfiguration _settings;
	private readonly ResponseBuilder _responseBuilder;

	private RequestConfiguration _pingAndSniffRequestConfiguration;		

	/// <inheritdoc cref="RequestPipeline" />
	internal DefaultRequestPipeline(
		TConfiguration configurationValues,
		DateTimeProvider dateTimeProvider,
		MemoryStreamFactory memoryStreamFactory,
		RequestParameters requestParameters
	)
	{
		_settings = configurationValues;
		_nodePool = _settings.NodePool;
		_transportClient = _settings.Connection;
		_dateTimeProvider = dateTimeProvider;
		_memoryStreamFactory = memoryStreamFactory;
		_productRegistration = configurationValues.ProductRegistration;
		_responseBuilder = _productRegistration.ResponseBuilder;
		_nodePredicate = _settings.NodePredicate ?? _productRegistration.NodePredicate;
		RequestConfiguration = requestParameters?.RequestConfiguration;
		StartedOn = dateTimeProvider.Now();
	}

	/// <inheritdoc cref="RequestPipeline.AuditTrail" />
	public override List<Audit> AuditTrail { get; } = new();

	private RequestConfiguration PingAndSniffRequestConfiguration
	{
		// Lazily loaded when first required, since not all node pools and configurations support pinging and sniffing.
		// This avoids allocating 192B per request for those which do not need to ping or sniff.
		get
		{
			if (_pingAndSniffRequestConfiguration is not null) return _pingAndSniffRequestConfiguration;

			_pingAndSniffRequestConfiguration = new RequestConfiguration
			{
				PingTimeout = PingTimeout,
				RequestTimeout = PingTimeout,
				AuthenticationHeader = _settings.Authentication,
				EnableHttpPipelining = RequestConfiguration?.EnableHttpPipelining ?? _settings.HttpPipeliningEnabled,
				ForceNode = RequestConfiguration?.ForceNode
			};

			return _pingAndSniffRequestConfiguration;
		}
	}

	//TODO xmldocs
#pragma warning disable 1591
	public bool DepletedRetries => Retried >= MaxRetries + 1 || IsTakingTooLong;

	public override bool FirstPoolUsageNeedsSniffing =>
		!RequestDisabledSniff
		&& _nodePool.SupportsReseeding && _settings.SniffsOnStartup && !_nodePool.SniffedOnStartup;

	public override bool IsTakingTooLong
	{
		get
		{
			var timeout = _settings.MaxRetryTimeout.GetValueOrDefault(RequestTimeout);
			var now = _dateTimeProvider.Now();

			//we apply a soft margin so that if a request timesout at 59 seconds when the maximum is 60 we also abort.
			var margin = timeout.TotalMilliseconds / 100.0 * 98;
			var marginTimeSpan = TimeSpan.FromMilliseconds(margin);
			var timespanCall = now - StartedOn;
			var tookToLong = timespanCall >= marginTimeSpan;
			return tookToLong;
		}
	}

	public override int MaxRetries =>
		RequestConfiguration?.ForceNode != null
			? 0
			: Math.Min(RequestConfiguration?.MaxRetries ?? _settings.MaxRetries.GetValueOrDefault(int.MaxValue), _nodePool.MaxRetries);

	public bool Refresh { get; private set; }

	public int Retried { get; private set; }

	public IEnumerable<Node> SniffNodes => _nodePool
		.CreateView(LazyAuditable)
		.ToList()
		.OrderBy(n => _productRegistration.SniffOrder(n));

	public override bool SniffsOnConnectionFailure =>
		!RequestDisabledSniff
		&& _nodePool.SupportsReseeding && _settings.SniffsOnConnectionFault;

	public override bool SniffsOnStaleCluster =>
		!RequestDisabledSniff
		&& _nodePool.SupportsReseeding && _settings.SniffInformationLifeSpan.HasValue;

	public override bool StaleClusterState
	{
		get
		{
			if (!SniffsOnStaleCluster) return false;

			// ReSharper disable once PossibleInvalidOperationException
			// already checked by SniffsOnStaleCluster
			var sniffLifeSpan = _settings.SniffInformationLifeSpan.Value;

			var now = _dateTimeProvider.Now();
			var lastSniff = _nodePool.LastUpdate;

			return sniffLifeSpan < now - lastSniff;
		}
	}

	public override DateTime StartedOn { get; }

	private TimeSpan PingTimeout =>
		RequestConfiguration?.PingTimeout
		?? _settings.PingTimeout
		?? (_nodePool.UsingSsl ? TransportConfiguration.DefaultPingTimeoutOnSsl : TransportConfiguration.DefaultPingTimeout);

	private IRequestConfiguration RequestConfiguration { get; }

	private bool RequestDisabledSniff => RequestConfiguration != null && (RequestConfiguration.DisableSniff ?? false);

	private TimeSpan RequestTimeout => RequestConfiguration?.RequestTimeout ?? _settings.RequestTimeout;

	public override void AuditCancellationRequested() => Audit(CancellationRequested).Dispose();

	public override void BadResponse<TResponse>(ref TResponse response, ApiCallDetails callDetails, RequestData data, TransportException exception)
	{
		if (response == null)
		{
			//make sure we copy over the error body in case we disabled direct streaming.
			var s = callDetails?.ResponseBodyInBytes == null ? Stream.Null : _memoryStreamFactory.Create(callDetails.ResponseBodyInBytes);
			var m = callDetails?.ResponseMimeType ?? RequestData.MimeType;
			response = _responseBuilder.ToResponse<TResponse>(data, exception, callDetails?.HttpStatusCode, null, s, m, callDetails?.ResponseBodyInBytes?.Length ?? -1, null, null);
		}

		if (response.ApiCallDetails is ApiCallDetails apiCallDetails)
			apiCallDetails.AuditTrail = AuditTrail;
	}

	public override TResponse CallProductEndpoint<TResponse>(RequestData requestData)
	{
		using (var audit = Audit(HealthyResponse, requestData.Node))
		using (var d = RequestPipelineStatics.DiagnosticSource.Diagnose<RequestData, ApiCallDetails>(
			DiagnosticSources.RequestPipeline.CallProductEndpoint, requestData))
		{
			audit.Path = requestData.PathAndQuery;
			try
			{
				var response = _transportClient.Request<TResponse>(requestData);
				d.EndState = response.ApiCallDetails;

				if (response.ApiCallDetails is ApiCallDetails apiCallDetails)
					apiCallDetails.AuditTrail = AuditTrail;

				ThrowBadAuthPipelineExceptionWhenNeeded(response.ApiCallDetails, response);
				if (!response.ApiCallDetails.HasSuccessfulStatusCode) audit.Event = requestData.OnFailureAuditEvent;
				return response;
			}
			catch (Exception e)
			{
				audit.Event = requestData.OnFailureAuditEvent;
				audit.Exception = e;
				throw;
			}
		}
	}

	public override async Task<TResponse> CallProductEndpointAsync<TResponse>(RequestData requestData, CancellationToken cancellationToken)
	{
		using (var audit = Audit(HealthyResponse, requestData.Node))
		using (var d = RequestPipelineStatics.DiagnosticSource.Diagnose<RequestData, ApiCallDetails>(
			DiagnosticSources.RequestPipeline.CallProductEndpoint, requestData))
		{
			audit.Path = requestData.PathAndQuery;
			try
			{
				var response = await _transportClient.RequestAsync<TResponse>(requestData, cancellationToken).ConfigureAwait(false);
				d.EndState = response.ApiCallDetails;

				if (response.ApiCallDetails is ApiCallDetails apiCallDetails)
					apiCallDetails.AuditTrail = AuditTrail;

				ThrowBadAuthPipelineExceptionWhenNeeded(response.ApiCallDetails, response);
				if (!response.ApiCallDetails.HasSuccessfulStatusCode) audit.Event = requestData.OnFailureAuditEvent;
				return response;
			}
			catch (Exception e)
			{
				audit.Event = requestData.OnFailureAuditEvent;
				audit.Exception = e;
				throw;
			}
		}
	}

	public override TransportException CreateClientException<TResponse>(
		TResponse response, ApiCallDetails callDetails, RequestData data, List<PipelineException> pipelineExceptions)
	{
		if (callDetails?.HasSuccessfulStatusCode ?? false) return null;

		var pipelineFailure = data.OnFailurePipelineFailure;
		var innerException = callDetails?.OriginalException;
		if (pipelineExceptions.HasAny(out var exs))
		{
			pipelineFailure = exs.Last().FailureReason;
			innerException = exs.AsAggregateOrFirst();
		}

		var statusCode = callDetails?.HttpStatusCode != null ? callDetails.HttpStatusCode.Value.ToString() : "unknown";
		var resource = callDetails == null
			? "unknown resource"
			: $"Status code {statusCode} from: {callDetails.HttpMethod} {callDetails.Uri.PathAndQuery}";

		var exceptionMessage = innerException?.Message ?? "Request failed to execute";

		if (IsTakingTooLong)
		{
			pipelineFailure = PipelineFailure.MaxTimeoutReached;
			Audit(MaxTimeoutReached);
			exceptionMessage = "Maximum timeout reached while retrying request";
		}
		else if (Retried >= MaxRetries && MaxRetries > 0)
		{
			pipelineFailure = PipelineFailure.MaxRetriesReached;
			Audit(MaxRetriesReached);
			exceptionMessage = "Maximum number of retries reached";

			var now = _dateTimeProvider.Now();
			var activeNodes = _nodePool.Nodes.Count(n => n.IsAlive || n.DeadUntil <= now);
			if (Retried >= activeNodes)
			{
				Audit(FailedOverAllNodes);
				exceptionMessage += ", failed over to all the known alive nodes before failing";
			}
		}

		exceptionMessage += !exceptionMessage.EndsWith(".", StringComparison.Ordinal) ? $". Call: {resource}" : $" Call: {resource}";
		if (response != null && _productRegistration.TryGetServerErrorReason(response, out var reason))
			exceptionMessage += $". ServerError: {reason}";

		var clientException = new TransportException(pipelineFailure, exceptionMessage, innerException)
		{
			Request = data, ApiCallDetails = callDetails, AuditTrail = AuditTrail
		};

		return clientException;
	}

	public override void FirstPoolUsage(SemaphoreSlim semaphore)
	{
		if (!FirstPoolUsageNeedsSniffing) return;

		if (!semaphore.Wait(_settings.RequestTimeout))
		{
			if (FirstPoolUsageNeedsSniffing)
				throw new PipelineException(PipelineFailure.CouldNotStartSniffOnStartup, null);

			return;
		}

		if (!FirstPoolUsageNeedsSniffing)
		{
			semaphore.Release();
			return;
		}

		try
		{
			using (Audit(SniffOnStartup))
			{
				Sniff();
				_nodePool.MarkAsSniffed();
			}
		}
		finally
		{
			semaphore.Release();
		}
	}

	public override async Task FirstPoolUsageAsync(SemaphoreSlim semaphore, CancellationToken cancellationToken)
	{
		if (!FirstPoolUsageNeedsSniffing) return;

		// TODO cancellationToken could throw here and will bubble out as OperationCancelledException
		// everywhere else it would bubble out wrapped in a `UnexpectedTransportException`
		var success = await semaphore.WaitAsync(_settings.RequestTimeout, cancellationToken).ConfigureAwait(false);
		if (!success)
		{
			if (FirstPoolUsageNeedsSniffing)
				throw new PipelineException(PipelineFailure.CouldNotStartSniffOnStartup, null);

			return;
		}

		if (!FirstPoolUsageNeedsSniffing)
		{
			semaphore.Release();
			return;
		}
		try
		{
			using (Audit(SniffOnStartup))
			{
				await SniffAsync(cancellationToken).ConfigureAwait(false);
				_nodePool.MarkAsSniffed();
			}
		}
		finally
		{
			semaphore.Release();
		}
	}

	public override void MarkAlive(Node node) => node.MarkAlive();

	public override void MarkDead(Node node)
	{
		var deadUntil = _dateTimeProvider.DeadTime(node.FailedAttempts, _settings.DeadTimeout, _settings.MaxDeadTimeout);
		node.MarkDead(deadUntil);
		Retried++;
	}

	/// <inheritdoc />
	public override bool TryGetSingleNode(out Node node)
	{
		if (_nodePool.Nodes.Count <= 1 && _nodePool.MaxRetries <= _nodePool.Nodes.Count &&
			!_nodePool.SupportsPinging && !_nodePool.SupportsReseeding)
		{
			node = _nodePool.Nodes.FirstOrDefault();

			if (node is not null && _nodePredicate(node)) return true;
		}

		node = null;
		return false;
	}

	public override IEnumerable<Node> NextNode()
	{
		if (RequestConfiguration?.ForceNode != null)
		{
			yield return new Node(RequestConfiguration.ForceNode);

			yield break;
		}

		//This for loop allows to break out of the view state machine if we need to
		//force a refresh (after reseeding node pool). We have a hardcoded limit of only
		//allowing 100 of these refreshes per call
		var refreshed = false;
		for (var i = 0; i < 100; i++)
		{
			if (DepletedRetries) yield break;

			foreach (var node in _nodePool.CreateView(LazyAuditable))
			{
				if (DepletedRetries) break;

				if (!_nodePredicate(node)) continue;

				yield return node;

				if (!Refresh) continue;

				Refresh = false;
				refreshed = true;
				break;
			}
			//unless a refresh was requested we will not iterate over more then a single view.
			//keep in mind refreshes are also still bound to overall maxretry count/timeout.
			if (!refreshed) break;
		}
	}

	public override void Ping(Node node)
	{
		if (!_productRegistration.SupportsPing) return;
		if (PingDisabled(node)) return;

		var pingData = _productRegistration.CreatePingRequestData(node, PingAndSniffRequestConfiguration, _settings, _memoryStreamFactory);
		using (var audit = Audit(PingSuccess, node))
		using (var d = RequestPipelineStatics.DiagnosticSource.Diagnose<RequestData, ApiCallDetails>(DiagnosticSources.RequestPipeline.Ping,
			pingData))
		{
			audit.Path = pingData.PathAndQuery;
			try
			{
				var response = _productRegistration.Ping(_transportClient, pingData);
				d.EndState = response.ApiCallDetails;
				ThrowBadAuthPipelineExceptionWhenNeeded(response.ApiCallDetails);
				//ping should not silently accept bad but valid http responses
				if (!response.ApiCallDetails.HasSuccessfulStatusCode)
					throw new PipelineException(pingData.OnFailurePipelineFailure, response.ApiCallDetails.OriginalException) { Response = response };
			}
			catch (Exception e)
			{
				var response = (e as PipelineException)?.Response;
				audit.Event = PingFailure;
				audit.Exception = e;
				throw new PipelineException(PipelineFailure.PingFailure, e) { Response = response };
			}
		}
	}

	public override async Task PingAsync(Node node, CancellationToken cancellationToken)
	{
		if (!_productRegistration.SupportsPing) return;
		if (PingDisabled(node)) return;

		var pingData = _productRegistration.CreatePingRequestData(node, PingAndSniffRequestConfiguration, _settings, _memoryStreamFactory);
		using (var audit = Audit(PingSuccess, node))
		using (var d = RequestPipelineStatics.DiagnosticSource.Diagnose<RequestData, ApiCallDetails>(DiagnosticSources.RequestPipeline.Ping,
			pingData))
		{
			audit.Path = pingData.PathAndQuery;
			try
			{
				var response = await _productRegistration.PingAsync(_transportClient, pingData, cancellationToken).ConfigureAwait(false);
				d.EndState = response.ApiCallDetails;
				ThrowBadAuthPipelineExceptionWhenNeeded(response.ApiCallDetails);
				//ping should not silently accept bad but valid http responses
				if (!response.ApiCallDetails.HasSuccessfulStatusCode)
					throw new PipelineException(pingData.OnFailurePipelineFailure, response.ApiCallDetails.OriginalException) { Response = response };
			}
			catch (Exception e)
			{
				var response = (e as PipelineException)?.Response;
				audit.Event = PingFailure;
				audit.Exception = e;
				throw new PipelineException(PipelineFailure.PingFailure, e) { Response = response };
			}
		}
	}

	public override void Sniff()
	{
		if (!_productRegistration.SupportsSniff) return;

		var exceptions = new List<Exception>();
		foreach (var node in SniffNodes)
		{
			var requestData =
				_productRegistration.CreateSniffRequestData(node, PingAndSniffRequestConfiguration, _settings, _memoryStreamFactory);
			using (var audit = Audit(SniffSuccess, node))
			using (var d = RequestPipelineStatics.DiagnosticSource.Diagnose<RequestData, ApiCallDetails>(DiagnosticSources.RequestPipeline.Sniff,
				requestData))
			using (RequestPipelineStatics.DiagnosticSource.Diagnose(DiagnosticSources.RequestPipeline.Sniff, requestData))
				try
				{
					audit.Path = requestData.PathAndQuery;
					var (response, nodes) = _productRegistration.Sniff(_transportClient, _nodePool.UsingSsl, requestData);
					d.EndState = response.ApiCallDetails;

					ThrowBadAuthPipelineExceptionWhenNeeded(response.ApiCallDetails);
					//sniff should not silently accept bad but valid http responses
					if (!response.ApiCallDetails.HasSuccessfulStatusCode)
						throw new PipelineException(requestData.OnFailurePipelineFailure, response.ApiCallDetails.OriginalException) { Response = response };

					_nodePool.Reseed(nodes);
					Refresh = true;
					return;
				}
				catch (Exception e)
				{
					audit.Event = SniffFailure;
					audit.Exception = e;
					exceptions.Add(e);
				}
		}

		throw new PipelineException(PipelineFailure.SniffFailure, exceptions.AsAggregateOrFirst());
	}

	public override async Task SniffAsync(CancellationToken cancellationToken)
	{
		if (!_productRegistration.SupportsSniff) return;

		var exceptions = new List<Exception>();
		foreach (var node in SniffNodes)
		{
			var requestData =
				_productRegistration.CreateSniffRequestData(node, PingAndSniffRequestConfiguration, _settings, _memoryStreamFactory);
			using (var audit = Audit(SniffSuccess, node))
			using (var d = RequestPipelineStatics.DiagnosticSource.Diagnose<RequestData, ApiCallDetails>(DiagnosticSources.RequestPipeline.Sniff,
				requestData))
				try
				{
					audit.Path = requestData.PathAndQuery;
					var (response, nodes) = await _productRegistration
						.SniffAsync(_transportClient, _nodePool.UsingSsl, requestData, cancellationToken)
						.ConfigureAwait(false);
					d.EndState = response.ApiCallDetails;

					ThrowBadAuthPipelineExceptionWhenNeeded(response.ApiCallDetails);
					//sniff should not silently accept bad but valid http responses
					if (!response.ApiCallDetails.HasSuccessfulStatusCode)
						throw new PipelineException(requestData.OnFailurePipelineFailure, response.ApiCallDetails.OriginalException) { Response = response };

					_nodePool.Reseed(nodes);
					Refresh = true;
					return;
				}
				catch (Exception e)
				{
					audit.Event = SniffFailure;
					audit.Exception = e;
					exceptions.Add(e);
				}
		}

		throw new PipelineException(PipelineFailure.SniffFailure, exceptions.AsAggregateOrFirst());
	}

	public override void SniffOnConnectionFailure()
	{
		if (!SniffsOnConnectionFailure) return;

		using (Audit(SniffOnFail))
			Sniff();
	}

	public override async Task SniffOnConnectionFailureAsync(CancellationToken cancellationToken)
	{
		if (!SniffsOnConnectionFailure) return;

		using (Audit(SniffOnFail))
			await SniffAsync(cancellationToken).ConfigureAwait(false);
	}

	public override void SniffOnStaleCluster()
	{
		if (!StaleClusterState) return;

		using (Audit(AuditEvent.SniffOnStaleCluster))
		{
			Sniff();
			_nodePool.MarkAsSniffed();
		}
	}

	public override async Task SniffOnStaleClusterAsync(CancellationToken cancellationToken)
	{
		if (!StaleClusterState) return;

		using (Audit(AuditEvent.SniffOnStaleCluster))
		{
			await SniffAsync(cancellationToken).ConfigureAwait(false);
			_nodePool.MarkAsSniffed();
		}
	}

	public override void ThrowNoNodesAttempted(RequestData requestData, List<PipelineException> seenExceptions)
	{
		var clientException = new TransportException(PipelineFailure.NoNodesAttempted, RequestPipelineStatics.NoNodesAttemptedMessage,
			(Exception)null);
		using (Audit(NoNodesAttempted))
			throw new UnexpectedTransportException(clientException, seenExceptions) { Request = requestData, AuditTrail = AuditTrail };
	}

	private bool PingDisabled(Node node) =>
		(RequestConfiguration?.DisablePing).GetValueOrDefault(false)
		|| _settings.DisablePings || !_nodePool.SupportsPinging || !node.IsResurrected;

	private Auditable Audit(AuditEvent type, Node node = null) => new(type, AuditTrail, _dateTimeProvider, node);

	private static void ThrowBadAuthPipelineExceptionWhenNeeded(ApiCallDetails details, TransportResponse response = null)
	{
		if (details?.HttpStatusCode == 401)
			throw new PipelineException(PipelineFailure.BadAuthentication, details.OriginalException) { Response = response };
	}

	private void LazyAuditable(AuditEvent e, Node n)
	{
		using (new Auditable(e, AuditTrail, _dateTimeProvider, n)) { }
	}
}
#pragma warning restore 1591
