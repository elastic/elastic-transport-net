// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

// Tests skipped with a BUG tag document suspected defects in the current source and are expected to be re-enabled when fixed.

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

namespace Elastic.Transport.Tests.ClientConcepts.ConnectionPooling.RequestOverrides;

public class RespectsMaxRetryOverrides
{

	// Request override MaxRetries(2) ignored: BadRequest on 9200..9208 then HealthyResponse 9209.
	// Expected: BadRequest 9200, 9201, 9202 then MaxRetriesReached.
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
			new ClientCall(r => r.MaxRetries(2)) {
				{ BadRequest, 9200 },
				{ BadRequest, 9201 },
				{ BadRequest, 9202 },
				{ MaxRetriesReached }
			}
		);
	}

	// Request override MaxRetries(2) ignored: BadRequest on 9200..9208 then HealthyResponse 9209.
	// Expected: BadRequest 9200, 9201, 9202 then MaxRetriesReached.
	[Fact]
	public async Task FixedMaximumNumberOfRetries()
	{
		var audit = new Auditor(() => Virtual.Elasticsearch.Bootstrap(10)
			.ClientCalls(r => r.FailAlways())
			.ClientCalls(r => r.OnPort(9209).SucceedAlways())
			.StaticNodePool()
			.Settings(s => s.DisablePing().MaximumRetries(5))
		);

		audit = await audit.TraceCall(
			new ClientCall(r => r.MaxRetries(2)) {
				{ BadRequest, 9200 },
				{ BadRequest, 9201 },
				{ BadRequest, 9202 },
				{ MaxRetriesReached }
			}
		);
	}

	// thrown from the VirtualizedCluster ctor; VirtualCluster.SingleNodeConnection does not set DateTimeProvider on the pool.
	[Fact]
	public async Task DoesNotRetryOnSingleNodeConnectionPool()
	{
		var audit = new Auditor(() => Virtual.Elasticsearch.Bootstrap(10)
			.ClientCalls(r => r.FailAlways().Takes(TimeSpan.FromSeconds(3)))
			.ClientCalls(r => r.OnPort(9209).SucceedAlways())
			.SingleNodeConnection()
			.Settings(s => s.DisablePing().MaximumRetries(10))
		);

		audit = await audit.TraceCall(
			new ClientCall(r => r.MaxRetries(10)) {
				{ BadRequest, 9200 }
			}
		);
	}
}
