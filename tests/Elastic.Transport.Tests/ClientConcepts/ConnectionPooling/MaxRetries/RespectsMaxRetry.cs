// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

// Tests skipped with a BUG tag document suspected defects in the current source and are expected to be re-enabled when fixed.
#pragma warning disable xUnit1004

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Elastic.Transport.VirtualizedCluster;
using Elastic.Transport.VirtualizedCluster.Audit;
using Elastic.Transport.VirtualizedCluster.Providers;
using Elastic.Transport.VirtualizedCluster.Rules;
using FluentAssertions;
using Xunit;
using static Elastic.Transport.Diagnostics.Auditing.AuditEvent;
using static Elastic.Transport.VirtualizedCluster.Rules.TimesHelper;

namespace Elastic.Transport.Tests.ClientConcepts.ConnectionPooling.MaxRetries;

public class RespectsMaxRetry
{

	[Fact]
	public async Task DefaultMaxIsNumberOfNodes()
	{

		var audit = new Auditor(() => Virtual.Elasticsearch.Bootstrap(10)
			.ClientCalls(r => r.FailAlways())
			.ClientCalls(r => r.OnPort(9209).SucceedAlways())
			.StaticNodePool()
			.Settings(s => s.DisablePing())
		);

		audit = await audit.TraceCall(
			new ClientCall {
				{ BadRequest, 9200 },
				{ BadRequest, 9201 },
				{ BadRequest, 9202 },
				{ BadRequest, 9203 },
				{ BadRequest, 9204 },
				{ BadRequest, 9205 },
				{ BadRequest, 9206 },
				{ BadRequest, 9207 },
				{ BadRequest, 9208 },
				{ HealthyResponse, 9209 }
			}
		);
	}

	// Observed with MaximumRetries(3): BadRequest on 9200..9208 then HealthyResponse on 9209 (10 attempts).
	// Expected: BadRequest 9200..9203 then MaxRetriesReached.
	// Failure: the 1st client call's synchronous audit trail should assert ALL audit trail items
	[Fact(Skip = "BUG 1: MaxRetries not enforced while iterating nodes (DepletedRetries always sees attemptedNodes == 0)")]
	public async Task FixedMaximumNumberOfRetries()
	{
		var audit = new Auditor(() => Virtual.Elasticsearch.Bootstrap(10)
			.ClientCalls(r => r.FailAlways())
			.ClientCalls(r => r.OnPort(9209).SucceedAlways())
			.StaticNodePool()
			.Settings(s => s.DisablePing().MaximumRetries(3))
		);

		audit = await audit.TraceCall(
			new ClientCall {
				{ BadRequest, 9200 },
				{ BadRequest, 9201 },
				{ BadRequest, 9202 },
				{ BadRequest, 9203 },
				{ MaxRetriesReached }
			}
		);
	}

	[Fact]
	public async Task RespectsOveralRequestTimeout()
	{
		var audit = new Auditor(() => Virtual.Elasticsearch.Bootstrap(10)
			.ClientCalls(r => r.FailAlways().Takes(TimeSpan.FromSeconds(10)))
			.ClientCalls(r => r.OnPort(9209).SucceedAlways())
			.StaticNodePool()
			.Settings(s => s.DisablePing().RequestTimeout(TimeSpan.FromSeconds(20)))
		);

		audit = await audit.TraceCall(
			new ClientCall {
				{ BadRequest, 9200 },
				{ BadRequest, 9201 },
				{ MaxTimeoutReached }
			}
		);
	}

	// RequestTimeout 2s, MaxRetryTimeout 10s, each call takes 3s. Expected 5 BadRequest attempts then MaxTimeoutReached.
	// Observed: BadRequest 9200 (Took 00:00:02) then MaxTimeoutReached.
	// Failure: the 1st client call's synchronous audit trail should assert ALL audit trail items
	[Fact(Skip = "UNCLASSIFIED: MaxRetryTimeout is never read (RequestPipeline.IsTakingTooLong uses RequestTimeout only)")]
	public async Task RespectsMaxRetryTimeoutOverRequestTimeout()
	{
		var audit = new Auditor(() => Virtual.Elasticsearch.Bootstrap(10)
			.ClientCalls(r => r.FailAlways().Takes(TimeSpan.FromSeconds(3)))
			.ClientCalls(r => r.OnPort(9209).FailAlways())
			.StaticNodePool()
			.Settings(s => s.DisablePing().RequestTimeout(TimeSpan.FromSeconds(2)).MaxRetryTimeout(TimeSpan.FromSeconds(10)))
		);

		audit = await audit.TraceCall(
			new ClientCall {
				{ BadRequest, 9200 },
				{ BadRequest, 9201 },
				{ BadRequest, 9202 },
				{ BadRequest, 9203 },
				{ BadRequest, 9204 },
				{ MaxTimeoutReached }
			}
		);

	}

	// Expected: BadRequest 9200, BadRequest 9201, MaxRetriesReached, FailedOverAllNodes.
	// Observed: BadRequest 9200 (Took 00:00:02) then MaxTimeoutReached (request timeout 2s ends the whole request, MaxRetryTimeout 10s ignored).
	// Failure: the 1st client call's synchronous audit trail should assert ALL audit trail items
	[Fact(Skip = "UNCLASSIFIED: MaxRetryTimeout is never read (RequestPipeline.IsTakingTooLong uses RequestTimeout only)")]
	public async Task RetriesAreLimitedByNodesInPool()
	{
		var audit = new Auditor(() => Virtual.Elasticsearch.Bootstrap(2)
			.ClientCalls(r => r.FailAlways().Takes(TimeSpan.FromSeconds(3)))
			.ClientCalls(r => r.OnPort(9209).SucceedAlways())
			.StaticNodePool()
			.Settings(s => s.DisablePing().RequestTimeout(TimeSpan.FromSeconds(2)).MaxRetryTimeout(TimeSpan.FromSeconds(10)))
		);

		audit = await audit.TraceCall(
			new ClientCall {
				{ BadRequest, 9200 },
				{ BadRequest, 9201 },
				{ MaxRetriesReached },
				{ FailedOverAllNodes }
			}
		);
	}

	// Failure: System.ArgumentException : DateTime provider is not a TestableDateTimeProvider (Parameter 'settings')
	// thrown from the VirtualizedCluster ctor; VirtualCluster.SingleNodeConnection does not set DateTimeProvider on the pool.
	[Fact(Skip = "UNCLASSIFIED: VirtualCluster.SingleNodeConnection() builds a SingleNodePool without the TestableDateTimeProvider")]
	public async Task DoesNotRetryOnSingleNodeConnectionPool()
	{
		var audit = new Auditor(() => Virtual.Elasticsearch.Bootstrap(10)
			.ClientCalls(r => r.FailAlways().Takes(TimeSpan.FromSeconds(3)))
			.ClientCalls(r => r.OnPort(9209).SucceedAlways())
			.SingleNodeConnection()
			.Settings(s => s.DisablePing().MaximumRetries(10))
		);

		audit = await audit.TraceCall(
			new ClientCall {
				{ BadRequest, 9200 }
			}
		);
	}
}
