// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Elastic.Transport.Diagnostics.Auditing;

namespace Elastic.Transport
{
	/// <summary> Models the workflow of a request to multiple nodes</summary>
	public interface IRequestPipeline : IDisposable // TODO - Should we move IDisposable to the implementation / Make this an abstract base type instead?
	{
		//TODO should not be List but requires a refactor
		/// <summary>
		/// An audit trail that can be used for logging and debugging purposes. Giving insights into how
		/// the request made its way through the workflow
		/// </summary>
		List<Audit> AuditTrail { get; }

		/// <summary>
		/// Should the workflow attempt the initial sniff as requested by
		/// <see cref="ITransportConfiguration.SniffsOnStartup" />
		/// </summary>
		bool FirstPoolUsageNeedsSniffing { get; }

//TODO xmldocs
#pragma warning disable 1591
		bool IsTakingTooLong { get; }

		int MaxRetries { get; }

		int Retried { get; }

		bool SniffsOnConnectionFailure { get; }

		bool SniffsOnStaleCluster { get; }

		bool StaleClusterState { get; }

		DateTime StartedOn { get; }

		TResponse CallProductEndpoint<TResponse>(RequestData requestData)
			where TResponse : TransportResponse, new();

		Task<TResponse> CallProductEndpointAsync<TResponse>(RequestData requestData, CancellationToken cancellationToken)
			where TResponse : TransportResponse, new();

		void MarkAlive(Node node);

		void MarkDead(Node node);

		/// <summary>
		/// Attempt to get a single node when the underlying connection pool contains only one node.
		/// <para>
		/// This provides an optimised path for single node pools by avoiding an Enumerator on each call.
		/// </para>
		/// </summary>
		/// <param name="node"></param>
		/// <returns><c>true</c> when a single node exists which has been set on the <paramref name="node" />.</returns>
		bool TryGetSingleNode(out Node node);

		IEnumerable<Node> NextNode();

		void Ping(Node node);

		Task PingAsync(Node node, CancellationToken cancellationToken);

		void FirstPoolUsage(SemaphoreSlim semaphore);

		Task FirstPoolUsageAsync(SemaphoreSlim semaphore, CancellationToken cancellationToken);

		void Sniff();

		Task SniffAsync(CancellationToken cancellationToken);

		void SniffOnStaleCluster();

		Task SniffOnStaleClusterAsync(CancellationToken cancellationToken);

		void SniffOnConnectionFailure();

		Task SniffOnConnectionFailureAsync(CancellationToken cancellationToken);

		void BadResponse<TResponse>(ref TResponse response, ApiCallDetails callDetails, RequestData data, TransportException exception)
			where TResponse : TransportResponse, new();

		void ThrowNoNodesAttempted(RequestData requestData, List<PipelineException> seenExceptions);

		void AuditCancellationRequested();

		TransportException CreateClientException<TResponse>(TResponse response, ApiCallDetails callDetails, RequestData data,
			List<PipelineException> seenExceptions
		)
			where TResponse : TransportResponse, new();
#pragma warning restore 1591
	}
}
