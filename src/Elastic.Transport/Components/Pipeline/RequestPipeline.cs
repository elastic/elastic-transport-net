// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Elastic.Transport.Diagnostics.Auditing;

namespace Elastic.Transport;

/// <summary>
/// Models the workflow of a request to multiple nodes
/// </summary>
public abstract class RequestPipeline : IDisposable
{
	private bool _disposed;

	internal RequestPipeline() { }

	/// <summary>
	/// An audit trail that can be used for logging and debugging purposes. Giving insights into how
	/// the request made its way through the workflow
	/// </summary>
	public abstract IEnumerable<Audit> AuditTrail { get; }

	/// <summary>
	/// Should the workflow attempt the initial sniff as requested by
	/// <see cref="ITransportConfiguration.SniffsOnStartup" />
	/// </summary>
	public abstract bool FirstPoolUsageNeedsSniffing { get; }

	//TODO xmldocs
#pragma warning disable 1591
	public abstract bool IsTakingTooLong { get; }

	public abstract int MaxRetries { get; }

	public abstract bool SniffsOnConnectionFailure { get; }

	public abstract bool SniffsOnStaleCluster { get; }

	public abstract bool StaleClusterState { get; }

	public abstract DateTimeOffset StartedOn { get; }

	public abstract TResponse CallProductEndpoint<TResponse>(Endpoint endpoint, RequestData requestData, PostData? postData)
		where TResponse : TransportResponse, new();

	public abstract Task<TResponse> CallProductEndpointAsync<TResponse>(Endpoint endpoint, RequestData requestData, PostData? postData, CancellationToken cancellationToken)
		where TResponse : TransportResponse, new();

	public abstract void MarkAlive(Node node);

	public abstract void MarkDead(Node node);

	/// <summary>
	/// Attempt to get a single node when the underlying connection pool contains only one node.
	/// <para>
	/// This provides an optimised path for single node pools by avoiding an Enumerator on each call.
	/// </para>
	/// </summary>
	/// <param name="node"></param>
	/// <returns><c>true</c> when a single node exists which has been set on the <paramref name="node" />.</returns>
	public abstract bool TryGetSingleNode(out Node node);

	public abstract IEnumerable<Node> NextNode();

	public abstract void Ping(Node node);

	public abstract Task PingAsync(Node node, CancellationToken cancellationToken);

	public abstract void FirstPoolUsage(SemaphoreSlim semaphore);

	public abstract Task FirstPoolUsageAsync(SemaphoreSlim semaphore, CancellationToken cancellationToken);

	public abstract void Sniff();

	public abstract Task SniffAsync(CancellationToken cancellationToken);

	public abstract void SniffOnStaleCluster();

	public abstract Task SniffOnStaleClusterAsync(CancellationToken cancellationToken);

	public abstract void SniffOnConnectionFailure();

	public abstract Task SniffOnConnectionFailureAsync(CancellationToken cancellationToken);

	public abstract void BadResponse<TResponse>(ref TResponse response, ApiCallDetails callDetails, Endpoint endpoint, RequestData data, PostData? postData, TransportException exception)
		where TResponse : TransportResponse, new();

	public abstract void ThrowNoNodesAttempted(Endpoint endpoint, List<PipelineException>? seenExceptions);

	public abstract void AuditCancellationRequested();

	public abstract TransportException? CreateClientException<TResponse>(TResponse? response, ApiCallDetails? callDetails,
		Endpoint endpoint, RequestData data, List<PipelineException>? seenExceptions)
		where TResponse : TransportResponse, new();
#pragma warning restore 1591

	/// <summary>
	///
	/// </summary>
	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

	/// <summary>
	///
	/// </summary>
	/// <param name="disposing"></param>
	protected virtual void Dispose(bool disposing)
	{
		if (_disposed)
			return;

		if (disposing)
		{
			DisposeManagedResources();
		}

		_disposed = true;
	}

	/// <summary>
	///
	/// </summary>
	protected virtual void DisposeManagedResources() { }
}
