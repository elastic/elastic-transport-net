// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Elastic.Transport.Diagnostics.Auditing;
using Elastic.Transport.Extensions;
using Elastic.Transport.Products;
using static Elastic.Transport.Diagnostics.Auditing.AuditEvent;

namespace Elastic.Transport;

/// Models the workflow of a request to multiple nodes
public class RequestPipeline
{
	private readonly IRequestInvoker _requestInvoker;
	private readonly NodePool _nodePool;
	private readonly BoundConfiguration _boundConfiguration;
	private readonly DateTimeProvider _dateTimeProvider;
	private readonly MemoryStreamFactory _memoryStreamFactory;
	private readonly Func<Node, bool> _nodePredicate;
	private readonly ProductRegistration _productRegistration;

	private RequestConfiguration? _pingAndSniffRequestConfiguration;

	private readonly ITransportConfiguration _settings;

	/// <inheritdoc cref="RequestPipeline" />
	internal RequestPipeline(BoundConfiguration boundConfiguration)
	{
		_boundConfiguration = boundConfiguration;
		_settings = boundConfiguration.ConnectionSettings;
		_nodePool = boundConfiguration.ConnectionSettings.NodePool;
		_requestInvoker = boundConfiguration.ConnectionSettings.RequestInvoker;
		_dateTimeProvider = boundConfiguration.ConnectionSettings.DateTimeProvider;
		_memoryStreamFactory = boundConfiguration.MemoryStreamFactory;
		_productRegistration = boundConfiguration.ConnectionSettings.ProductRegistration;
		_nodePredicate = boundConfiguration.ConnectionSettings.NodePredicate ?? _productRegistration.NodePredicate;
	}

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
				Authentication = _boundConfiguration.AuthenticationHeader,
				HttpPipeliningEnabled = _boundConfiguration.HttpPipeliningEnabled,
				ForceNode = _boundConfiguration.ForceNode
			};

			return _pingAndSniffRequestConfiguration;
		}
	}

	private bool DepletedRetries(DateTimeOffset startedOn) => Retried >= MaxRetries + 1 || IsTakingTooLong(startedOn);

	private bool FirstPoolUsageNeedsSniffing =>
		!RequestDisabledSniff
		&& _nodePool.SupportsReseeding && _settings.SniffsOnStartup && !_nodePool.SniffedOnStartup;

	private bool IsTakingTooLong(DateTimeOffset startedOn)
	{
		var timeout = RequestTimeout;
		var now = _dateTimeProvider.Now();

		//we apply a soft margin so that if a request times out at 59 seconds when the maximum is 60 we also abort.
		var margin = timeout.TotalMilliseconds / 100.0 * 98;
		var marginTimeSpan = TimeSpan.FromMilliseconds(margin);
		var timespanCall = now - startedOn;
		var tookToLong = timespanCall >= marginTimeSpan;
		return tookToLong;
	}

	private int MaxRetries => _boundConfiguration.MaxRetries;

	private bool Refresh { get; set; }

	private int Retried { get; set; }

	private IEnumerable<Node> SniffNodes(Auditor? auditor) => _nodePool
		.CreateView(auditor)
		.ToList()
		.OrderBy(n => _productRegistration.SniffOrder(n));

	private bool SniffsOnConnectionFailure =>
		!RequestDisabledSniff
		&& _nodePool.SupportsReseeding && _settings.SniffsOnConnectionFault;

	private bool SniffsOnStaleCluster =>
		!RequestDisabledSniff
		&& _nodePool.SupportsReseeding && _settings.SniffInformationLifeSpan.HasValue;

	private bool StaleClusterState
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

	private TimeSpan PingTimeout => _boundConfiguration.PingTimeout;

	private bool RequestDisabledSniff => _boundConfiguration.DisableSniff;

	private TimeSpan RequestTimeout => _boundConfiguration.RequestTimeout;

	/// Emit <see cref="AuditEvent.CancellationRequested"/> event
	public void AuditCancellationRequested(Auditor? auditor) => auditor?.Emit(CancellationRequested);

	/// Ensures a response is returned with <see cref="ApiCallDetails"/>
	public void BadResponse<TResponse>(
		ref TResponse? response,
		ApiCallDetails? callDetails,
		Endpoint endpoint,
		BoundConfiguration boundConfiguration,
		PostData? postData,
		TransportException exception,
		IReadOnlyCollection<Audit>? auditTrail
	)
		where TResponse : TransportResponse, new()
	{
		if (response == null)
		{
			//make sure we copy over the error body in case we disabled direct streaming.
			var s = callDetails?.ResponseBodyInBytes == null ? Stream.Null : _memoryStreamFactory.Create(callDetails.ResponseBodyInBytes);
			var m = callDetails?.ResponseContentType ?? BoundConfiguration.DefaultContentType;
			response = _requestInvoker.ResponseFactory.Create<TResponse>(endpoint, boundConfiguration, postData, exception, callDetails?.HttpStatusCode, null, s, m, callDetails?.ResponseBodyInBytes?.Length ?? -1, null, null);
		}

		response.ApiCallDetails.AuditTrail = auditTrail;
	}

	/// Call the product's API endpoint ensuring rich enough exceptions are thrown
	public TResponse CallProductEndpoint<TResponse>(Endpoint endpoint, BoundConfiguration boundConfiguration, PostData? postData, Auditor? auditor)
		where TResponse : TransportResponse, new()
		=> CallProductEndpointCoreAsync<TResponse>(false, endpoint, boundConfiguration, postData, auditor).EnsureCompleted();

	/// Call the product's API endpoint ensuring rich enough exceptions are thrown
	public Task<TResponse> CallProductEndpointAsync<TResponse>(Endpoint endpoint, BoundConfiguration boundConfiguration, PostData? postData, Auditor? auditor, CancellationToken cancellationToken = default)
		where TResponse : TransportResponse, new()
		=> CallProductEndpointCoreAsync<TResponse>(true, endpoint, boundConfiguration, postData, auditor, cancellationToken).AsTask();

	private async ValueTask<TResponse> CallProductEndpointCoreAsync<TResponse>(
		bool isAsync, Endpoint endpoint, BoundConfiguration boundConfiguration, PostData? postData, Auditor? auditor, CancellationToken cancellationToken = default)
		where TResponse : TransportResponse, new()
	{
		using var audit = auditor?.Add(HealthyResponse, _dateTimeProvider, endpoint.Node);

		try
		{
			TResponse response;

			if (isAsync)
				response = await _requestInvoker.RequestAsync<TResponse>(endpoint, boundConfiguration, postData, cancellationToken).ConfigureAwait(false);
			else
				response = _requestInvoker.Request<TResponse>(endpoint, boundConfiguration, postData);

			response.ApiCallDetails.AuditTrail = auditor;

			ThrowBadAuthPipelineExceptionWhenNeeded(response.ApiCallDetails, response);

			if (!response.ApiCallDetails.HasSuccessfulStatusCodeAndExpectedContentType && audit is not null)
			{
				var @event = response.ApiCallDetails.HttpStatusCode != null ? AuditEvent.BadResponse : BadRequest;
				audit.Event = @event;
			}

			return response;
		}
		catch (Exception e) when (audit is not null)
		{
			var @event = e is TransportException t && t.ApiCallDetails.HttpStatusCode != null ? AuditEvent.BadResponse : BadRequest;
			audit.Event = @event;
			audit.Exception = e;
			throw;
		}
	}

	/// Create a rich enough <see cref="TransportException"/>
	public TransportException? CreateClientException<TResponse>(
		TResponse response,
		ApiCallDetails? callDetails,
		Endpoint endpoint,
		Auditor? auditor,
		DateTimeOffset startedOn,
		List<PipelineException>? seenExceptions
	)
		where TResponse : TransportResponse, new()
	{
		if (callDetails?.HasSuccessfulStatusCodeAndExpectedContentType ?? false) return null;

		var pipelineFailure = callDetails?.HttpStatusCode != null ? PipelineFailure.BadResponse : PipelineFailure.BadRequest;
		var innerException = callDetails?.OriginalException;
		if (seenExceptions is not null && seenExceptions.HasAny(out var exs))
		{
			pipelineFailure = exs.Last().FailureReason;
			innerException = exs.AsAggregateOrFirst();
		}

		var statusCode = callDetails?.HttpStatusCode != null ? callDetails.HttpStatusCode.Value.ToString() : "unknown";
		var resource = callDetails == null
			? "unknown resource"
			: $"Status code {statusCode} from: {callDetails.HttpMethod} {callDetails.Uri.PathAndQuery}";

		var exceptionMessage = innerException?.Message ?? "Request failed to execute";

		if (IsTakingTooLong(startedOn))
		{
			pipelineFailure = PipelineFailure.MaxTimeoutReached;
			auditor?.Emit(MaxTimeoutReached);
			exceptionMessage = "Maximum timeout reached while retrying request";
		}
		else if (Retried >= MaxRetries && MaxRetries > 0)
		{
			pipelineFailure = PipelineFailure.MaxRetriesReached;
			auditor?.Emit(MaxRetriesReached);
			exceptionMessage = "Maximum number of retries reached";

			var now = _dateTimeProvider.Now();
			var activeNodes = _nodePool.Nodes.Count(n => n.IsAlive || n.DeadUntil <= now);
			if (Retried >= activeNodes)
			{
				auditor?.Emit(FailedOverAllNodes);
				exceptionMessage += ", failed over to all the known alive nodes before failing";
			}
		}

		exceptionMessage += !exceptionMessage.EndsWith(".", StringComparison.Ordinal) ? $". Call: {resource}" : $" Call: {resource}";
		if (response != null && _productRegistration.TryGetServerErrorReason(response, out var reason))
			exceptionMessage += $". ServerError: {reason}";

		var clientException = new TransportException(pipelineFailure, exceptionMessage, innerException)
		{
			Endpoint = endpoint,
			ApiCallDetails = callDetails,
			AuditTrail = auditor
		};

		return clientException;
	}

	/// Routine for the first call into the product, potentially sniffing to discover the network topology
	public void FirstPoolUsage(SemaphoreSlim semaphore, Auditor? auditor)
	{
		if (!FirstPoolUsageNeedsSniffing) return;

		if (!semaphore.Wait(RequestTimeout))
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
			using (auditor?.Add(SniffOnStartup, _dateTimeProvider))
			{
				Sniff(auditor);
				_nodePool.MarkAsSniffed();
			}
		}
		finally
		{
			semaphore.Release();
		}
	}

	/// <inheritdoc cref="FirstPoolUsage"/>
	public async Task FirstPoolUsageAsync(SemaphoreSlim semaphore, Auditor? auditor, CancellationToken cancellationToken)
	{
		if (!FirstPoolUsageNeedsSniffing) return;

		// TODO cancellationToken could throw here and will bubble out as OperationCancelledException
		// everywhere else it would bubble out wrapped in a `UnexpectedTransportException`
		var success = await semaphore.WaitAsync(RequestTimeout, cancellationToken).ConfigureAwait(false);
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
			using (auditor?.Add(SniffOnStartup, _dateTimeProvider))
			{
				await SniffAsync(auditor, cancellationToken).ConfigureAwait(false);
				_nodePool.MarkAsSniffed();
			}
		}
		finally
		{
			semaphore.Release();
		}
	}

	/// Mark <paramref name="node"/> as alive putting it back in rotation.
	public void MarkAlive(Node node) => node.MarkAlive();

	/// Mark <paramref name="node"/> as dead, taking it out of rotation.
	public void MarkDead(Node node)
	{
		var deadUntil = _dateTimeProvider.DeadTime(node.FailedAttempts, _settings.DeadTimeout, _settings.MaxDeadTimeout);
		node.MarkDead(deadUntil);
		Retried++;
	}

	/// Fast path for <see cref="NextNode"/> if only a single node could ever be yielded this save an IEnumerator allocation
	public bool TryGetSingleNode(out Node? node)
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

	/// returns a consistent enumerable view into the available nodes
	public IEnumerable<Node> NextNode(DateTimeOffset startedOn, Auditor? auditor)
	{
		if (_boundConfiguration.ForceNode != null)
		{
			yield return new Node(_boundConfiguration.ForceNode);

			yield break;
		}

		//This for loop allows to break out of the view state machine if we need to
		//force a refresh (after reseeding node pool). We have a hardcoded limit of only
		//allowing 100 of these refreshes per call
		var refreshed = false;
		for (var i = 0; i < 100; i++)
		{
			if (DepletedRetries(startedOn)) yield break;

			foreach (var node in _nodePool.CreateView(auditor))
			{
				if (DepletedRetries(startedOn)) break;

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

	/// ping <paramref name="node"/> as a fast path ensuring its alive
	public void Ping(Node node, Auditor? auditor) => PingCoreAsync(false, node, auditor).EnsureCompleted();

	/// ping <paramref name="node"/> as a fast path ensuring its alive
	public Task PingAsync(Node node, Auditor? auditor, CancellationToken cancellationToken = default)
		=> PingCoreAsync(true, node, auditor, cancellationToken).AsTask();

	private async ValueTask PingCoreAsync(bool isAsync, Node node, Auditor? auditor, CancellationToken cancellationToken = default)
	{
		if (!_productRegistration.SupportsPing) return;
		if (PingDisabled(node)) return;

		var pingEndpoint = _productRegistration.CreatePingEndpoint(node, PingAndSniffRequestConfiguration);

		using var audit = auditor?.Add(PingSuccess, _dateTimeProvider, node);

		TransportResponse response;

		try
		{
			if (isAsync)
				response = await _productRegistration.PingAsync(_requestInvoker, pingEndpoint, _boundConfiguration, cancellationToken).ConfigureAwait(false);
			else
				response = _productRegistration.Ping(_requestInvoker, pingEndpoint, _boundConfiguration);

			ThrowBadAuthPipelineExceptionWhenNeeded(response.ApiCallDetails);

			//ping should not silently accept bad but valid http responses
			if (!response.ApiCallDetails.HasSuccessfulStatusCodeAndExpectedContentType)
			{
				var pipelineFailure = response.ApiCallDetails.HttpStatusCode != null ? PipelineFailure.BadResponse : PipelineFailure.BadRequest;
				throw new PipelineException(pipelineFailure, response.ApiCallDetails.OriginalException) { Response = response };
			}
		}
		catch (Exception e)
		{
			response = (e as PipelineException)?.Response;
			if (audit is not null)
			{
				audit.Event = PingFailure;
				audit.Exception = e;
			}
			throw new PipelineException(PipelineFailure.PingFailure, e) { Response = response };
		}
	}

	/// Discover the products network topology to yield all available nodes
	public void Sniff(Auditor? auditor) => SniffCoreAsync(false, auditor).EnsureCompleted();

	/// Discover the products network topology to yield all available nodes
	public Task SniffAsync(Auditor? auditor, CancellationToken cancellationToken = default)
		=> SniffCoreAsync(true, auditor, cancellationToken).AsTask();

	private async ValueTask SniffCoreAsync(bool isAsync, Auditor? auditor, CancellationToken cancellationToken = default)
	{
		if (!_productRegistration.SupportsSniff) return;

		var exceptions = new List<Exception>();

		foreach (var node in SniffNodes(auditor))
		{
			var sniffEndpoint = _productRegistration.CreateSniffEndpoint(node, PingAndSniffRequestConfiguration, _settings);
			using var audit = auditor?.Add(SniffSuccess, _dateTimeProvider, node);

			Tuple<TransportResponse, IReadOnlyCollection<Node>> result;

			try
			{
				if (isAsync)
					result = await _productRegistration
						.SniffAsync(_requestInvoker, _nodePool.UsingSsl, sniffEndpoint, _boundConfiguration, cancellationToken)
						.ConfigureAwait(false);
				else
					result = _productRegistration
						.Sniff(_requestInvoker, _nodePool.UsingSsl, sniffEndpoint, _boundConfiguration);

				ThrowBadAuthPipelineExceptionWhenNeeded(result.Item1.ApiCallDetails);

				//sniff should not silently accept bad but valid http responses
				if (!result.Item1.ApiCallDetails.HasSuccessfulStatusCodeAndExpectedContentType)
				{
					var pipelineFailure = result.Item1.ApiCallDetails.HttpStatusCode != null ? PipelineFailure.BadResponse : PipelineFailure.BadRequest;
					throw new PipelineException(pipelineFailure, result.Item1.ApiCallDetails.OriginalException) { Response = result.Item1 };
				}

				_nodePool.Reseed(result.Item2);
				Refresh = true;

				return;
			}
			catch (Exception e)
			{
				if (audit is not null)
				{
					audit.Event = SniffFailure;
					audit.Exception = e;
				}
				exceptions.Add(e);
			}

			throw new PipelineException(PipelineFailure.SniffFailure, exceptions.AsAggregateOrFirst());
		}
	}

	/// sniff the topology when a connection failure happens
	public void SniffOnConnectionFailure(Auditor? auditor)
	{
		if (!SniffsOnConnectionFailure) return;

		using (auditor?.Add(SniffOnFail, _dateTimeProvider))
			Sniff(auditor);
	}

	/// sniff the topology when a connection failure happens
	public async Task SniffOnConnectionFailureAsync(Auditor? auditor, CancellationToken cancellationToken)
	{
		if (!SniffsOnConnectionFailure) return;

		using (auditor?.Add(SniffOnFail, _dateTimeProvider))
			await SniffAsync(auditor, cancellationToken).ConfigureAwait(false);
	}

	/// sniff the topology after a set period to ensure it's up to date
	public void SniffOnStaleCluster(Auditor? auditor)
	{
		if (!StaleClusterState) return;

		using (auditor?.Add(AuditEvent.SniffOnStaleCluster, _dateTimeProvider))
		{
			Sniff(auditor);
			_nodePool.MarkAsSniffed();
		}
	}

	/// sniff the topology after a set period to ensure its up to date
	public async Task SniffOnStaleClusterAsync(Auditor? auditor, CancellationToken cancellationToken)
	{
		if (!StaleClusterState) return;

		using (auditor?.Add(AuditEvent.SniffOnStaleCluster, _dateTimeProvider))
		{
			await SniffAsync(auditor, cancellationToken).ConfigureAwait(false);
			_nodePool.MarkAsSniffed();
		}
	}

	/// emit <see cref="AuditEvent.NoNodesAttempted"/> event in case no nodes were available
	public void ThrowNoNodesAttempted(Endpoint endpoint, Auditor? auditor, List<PipelineException>? seenExceptions)
	{
		var clientException = new TransportException(PipelineFailure.NoNodesAttempted, RequestPipelineStatics.NoNodesAttemptedMessage);
		using (auditor?.Add(NoNodesAttempted, _dateTimeProvider))
			throw new UnexpectedTransportException(clientException, seenExceptions) { Endpoint = endpoint, AuditTrail = auditor };
	}

	private bool PingDisabled(Node node) => _boundConfiguration.DisablePings || !node.IsResurrected;

	private static void ThrowBadAuthPipelineExceptionWhenNeeded(ApiCallDetails details, TransportResponse? response = null)
	{
		if (details.HttpStatusCode == 401)
			throw new PipelineException(PipelineFailure.BadAuthentication, details.OriginalException) { Response = response };
	}
}
