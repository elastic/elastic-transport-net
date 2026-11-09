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

public class Revival
{

	[Fact]
	public async Task PingAfterRevival()
	{
		var audit = new Auditor(() => Virtual.Elasticsearch.Bootstrap(3)
			.ClientCalls(r => r.SucceedAlways())
			.ClientCalls(r => r.OnPort(9202).Fails(Once))
			.Ping(p => p.SucceedAlways())
			.StaticNodePool()
			.AllDefaults()
		);

		audit = await audit.TraceCalls(
			new ClientCall { { PingSuccess, 9200 }, { HealthyResponse, 9200 } },
			new ClientCall { { PingSuccess, 9201 }, { HealthyResponse, 9201 } },
			new ClientCall {
				{ PingSuccess, 9202},
				{ BadRequest, 9202},
				{ HealthyResponse, 9200},
				{ pool =>  pool.Nodes.Where(n=>!n.IsAlive).Should().HaveCount(1) }
			},
			new ClientCall { { HealthyResponse, 9201 } },
			new ClientCall { { HealthyResponse, 9200 } },
			new ClientCall { { HealthyResponse, 9201 } },
			new ClientCall {
				{ HealthyResponse, 9200 },
				{ pool => pool.Nodes.First(n=>!n.IsAlive).DeadUntil.Should().BeAfter(DateTimeOffset.UtcNow) }
			}
		);

		audit = await audit.TraceCalls(
			new ClientCall { { HealthyResponse, 9201 } },
			new ClientCall { { HealthyResponse, 9200 } },
			new ClientCall { { HealthyResponse, 9201 } }
		);

		audit.ChangeTime(d => d.AddMinutes(20));

		audit = await audit.TraceCalls(
			new ClientCall { { HealthyResponse, 9201 } },
			new ClientCall {
				{ Resurrection, 9202 },
				{ PingSuccess, 9202 },
				{ HealthyResponse, 9202 }
			}
		);
	}
}
