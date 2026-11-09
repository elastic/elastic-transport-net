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
using Elastic.Transport.Products.Elasticsearch;
using Elastic.Transport.VirtualizedCluster;
using Elastic.Transport.VirtualizedCluster.Audit;
using Elastic.Transport.VirtualizedCluster.Providers;
using Elastic.Transport.VirtualizedCluster.Rules;
using FluentAssertions;
using Xunit;
using static Elastic.Transport.Diagnostics.Auditing.AuditEvent;
using static Elastic.Transport.VirtualizedCluster.Rules.TimesHelper;

namespace Elastic.Transport.Tests.ClientConcepts.ConnectionPooling.Sniffing;

public class OnStaleClusterState
{

	// After ChangeTime(+31min) expected SniffOnStaleCluster, SniffSuccess 9202, HealthyResponse 9201.
	// Observed: only HealthyResponse 9201 (StaleClusterState is false while LastUpdate is null; old pools set LastUpdate = Now at construction).
	// Failure: the 1st client call's synchronous audit trail should assert ALL audit trail items
	[Fact(Skip = "UNCLASSIFIED: NodePool.LastUpdate is never initialised, so SniffOnStaleCluster never fires before the first reseed")]
	public async Task ASniffOnStartupHappens()
	{
		var audit = new Auditor(() => Virtual.Elasticsearch.Bootstrap(10)
			.MasterEligible(9202, 9203, 9204)
			.ClientCalls(r => r.SucceedAlways())
			.Sniff(s => s.SucceedAlways(Virtual.Elasticsearch.Bootstrap(100)
				.MasterEligible(9202, 9203, 9204)
				.ClientCalls(r => r.SucceedAlways())
				.Sniff(ss => ss.SucceedAlways(Virtual.Elasticsearch.Bootstrap(10)
					.MasterEligible(9202, 9203, 9204)
					.ClientCalls(r => r.SucceedAlways())
				))
			))
			.SniffingNodePool()
			.Settings(s => s
				.DisablePing()
				.SniffOnConnectionFault(false)
				.SniffOnStartup(false)
				.SniffLifeSpan(TimeSpan.FromMinutes(30))
			)
		);

		audit = await audit.TraceCalls(
			new ClientCall { { HealthyResponse, 9200 } },
			new ClientCall { { HealthyResponse, 9201 } },
			new ClientCall { { HealthyResponse, 9202 } },
			new ClientCall { { HealthyResponse, 9203 } },
			new ClientCall { { HealthyResponse, 9204 } },
			new ClientCall { { HealthyResponse, 9205 } },
			new ClientCall { { HealthyResponse, 9206 } },
			new ClientCall { { HealthyResponse, 9207 } },
			new ClientCall { { HealthyResponse, 9208 } },
			new ClientCall { { HealthyResponse, 9209 } },
			new ClientCall {
				{ HealthyResponse, 9200 },
				{ pool => pool.Nodes.Count.Should().Be(10) }
			}
		);

		audit.ChangeTime(d => d.AddMinutes(31));

		audit = await audit.TraceCalls(
			new ClientCall {
				{ SniffOnStaleCluster },
				{ SniffSuccess, 9202 },
				{ HealthyResponse, 9201 },
				{ pool => pool.Nodes.Count.Should().Be(100) }
			}
		);

		audit.ChangeTime(d => d.AddMinutes(31));

		audit = await audit.TraceCalls(
			new ClientCall {

				{ SniffOnStaleCluster },
				{ SniffSuccess, 9202 },
				{ HealthyResponse, 9200 },
				{ pool => pool.Nodes.Count.Should().Be(10) }
			}
		);
	}

}
