// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

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

namespace Elastic.Transport.Tests.ClientConcepts.ConnectionPooling.Pinging;

public class FirstUsage
{

	[Fact]
	public async Task PingFailsFallsOverToHealthyNodeWithoutPing()
	{

		var audit = new Auditor(() => Virtual.Elasticsearch.Bootstrap(2)
			.Ping(p => p.Succeeds(Always))
			.Ping(p => p.OnPort(9201).FailAlways())
			.ClientCalls(c=>c.SucceedAlways())
			.StaticNodePool()
			.AllDefaults()
		);

		await audit.TraceCalls(

			new ClientCall {
				{ PingSuccess, 9200},
				{ HealthyResponse, 9200},
				{ pool =>
				{
					pool.Nodes.Where(n=>!n.IsAlive).Should().HaveCount(0);
				} }
			},
			new ClientCall {
				{ PingFailure, 9201},
				{ HealthyResponse, 9200},
				{ pool =>  pool.Nodes.Where(n=>!n.IsAlive).Should().HaveCount(1) }
			}
		);
	}
	[Fact]
	public async Task PingFailsFallsOverMultipleTimesToHealthyNode()
	{

		var audit = new Auditor(() => Virtual.Elasticsearch.Bootstrap(4)
			.Ping(p => p.SucceedAlways())
			.Ping(p => p.OnPort(9201).FailAlways())
			.Ping(p => p.OnPort(9202).FailAlways())
			.ClientCalls(c=>c.SucceedAlways())
			.StaticNodePool()
			.AllDefaults()
		);

		await audit.TraceCalls(
			new ClientCall {
				{ PingSuccess, 9200},
				{ HealthyResponse, 9200},
				{ pool =>
					pool.Nodes.Where(n=>!n.IsAlive).Should().HaveCount(0)
				}
			},
			new ClientCall {
				{ PingFailure, 9201},
				{ PingFailure, 9202},
				{ PingSuccess, 9203},
				{ HealthyResponse, 9203},
				{ pool =>
					pool.Nodes.Where(n=>!n.IsAlive).Should().HaveCount(2)
				}
			}
		);
	}

	[Fact]
	public async Task AllNodesArePingedOnlyOnFirstUseProvidedTheyAreHealthy()
	{
		var audit = new Auditor(() => Virtual.Elasticsearch.Bootstrap(4)
			.Ping(p => p.SucceedAlways())
			.ClientCalls(c=>c.SucceedAlways())
			.StaticNodePool()
			.AllDefaults()
		);

		await audit.TraceCalls(
			new ClientCall { { PingSuccess, 9200}, { HealthyResponse, 9200} },
			new ClientCall { { PingSuccess, 9201}, { HealthyResponse, 9201} },
			new ClientCall { { PingSuccess, 9202}, { HealthyResponse, 9202} },
			new ClientCall { { PingSuccess, 9203}, { HealthyResponse, 9203} },
			new ClientCall { { HealthyResponse, 9200} },
			new ClientCall { { HealthyResponse, 9201} },
			new ClientCall { { HealthyResponse, 9202} },
			new ClientCall { { HealthyResponse, 9203} },
			new ClientCall { { HealthyResponse, 9200} }
		);
	}
}
