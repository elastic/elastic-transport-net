/*
 * Licensed to Elasticsearch B.V. under one or more contributor
 * license agreements. See the NOTICE file distributed with
 * this work for additional information regarding copyright
 * ownership. Elasticsearch B.V. licenses this file to you under
 * the Apache License, Version 2.0 (the "License"); you may
 * not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *    http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing,
 * software distributed under the License is distributed on an
 * "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
 * KIND, either express or implied.  See the License for the
 * specific language governing permissions and limitations
 * under the License.
 */

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Elastic.Transport.Diagnostics.Auditing;

namespace Elastic.Transport
{
	/// <summary> Models the workflow of a request to multiple nodes</summary>
	public interface IRequestPipeline : IDisposable
	{
		//TODO should not be List but requires a refactor
		/// <summary>
		/// An audit trail that can be used for logging and debugging purposes. Giving insights into how
		/// the request made its way through the workflow
		/// </summary>
		List<Audit> AuditTrail { get; }

		/// <summary> Should the workflow attempt the initial sniff as requested by
		/// <see cref="ITransportConfiguration.SniffsOnStartup"/>
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
			where TResponse : class, ITransportResponse, new();

		Task<TResponse> CallProductEndpointAsync<TResponse>(RequestData requestData, CancellationToken cancellationToken)
			where TResponse : class, ITransportResponse, new();

		void MarkAlive(Node node);

		void MarkDead(Node node);

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

		void BadResponse<TResponse>(ref TResponse response, IApiCallDetails callDetails, RequestData data, TransportException exception)
			where TResponse : class, ITransportResponse, new();

		void ThrowNoNodesAttempted(RequestData requestData, List<PipelineException> seenExceptions);

		void AuditCancellationRequested();

		TransportException CreateClientException<TResponse>(TResponse response, IApiCallDetails callDetails, RequestData data,
			List<PipelineException> seenExceptions
		)
			where TResponse : class, ITransportResponse, new();
#pragma warning restore 1591
	}
}
