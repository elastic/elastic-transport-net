// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

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

public class OnConnectionFailure
{

	[Fact]
	public async Task DoesASniffAfterConnectionFailure()
	{

		var audit = new Auditor(() => Virtual.Elasticsearch.Bootstrap(5)
			.MasterEligible(9202, 9203, 9204)
			.ClientCalls(r => r.SucceedAlways())
			.ClientCalls(r => r.OnPort(9201).Fails(Once))
			.Sniff(p => p.SucceedAlways(Virtual.Elasticsearch.Bootstrap(3)
				.MasterEligible(9200, 9202)
				.ClientCalls(r => r.OnPort(9201).Fails(Once))
				.ClientCalls(r => r.SucceedAlways())
				.Sniff(s => s.SucceedAlways(Virtual.Elasticsearch
					.Bootstrap(3, 9210)
					.MasterEligible(9210, 9212)
					.ClientCalls(r => r.SucceedAlways())
					.Sniff(r => r.SucceedAlways())
				))
			))
			.SniffingNodePool()
			.Settings(s => s.DisablePing().SniffOnStartup(false))
		);

		audit = await audit.TraceCalls(

			new ClientCall {
				{ HealthyResponse, 9200 },
				{ pool =>  pool.Nodes.Count.Should().Be(5) }
			},
			new ClientCall {
				{ BadRequest, 9201},
				{ SniffOnFail },
				{ SniffSuccess, 9202},
				{ HealthyResponse, 9200},
				{ pool =>  pool.Nodes.Count.Should().Be(3) }
			},
			new ClientCall {
				{ BadRequest, 9201},
				{ SniffOnFail },
				{ SniffSuccess, 9200},
				{ HealthyResponse, 9210},
				{ pool =>  pool.Nodes.Count.Should().Be(3) }
			},
			new ClientCall { { HealthyResponse, 9211 } },
			new ClientCall { { HealthyResponse, 9212 } },
			new ClientCall { { HealthyResponse, 9210 } },
			new ClientCall { { HealthyResponse, 9211 } },
			new ClientCall { { HealthyResponse, 9212 } },
			new ClientCall { { HealthyResponse, 9210 } },
			new ClientCall { { HealthyResponse, 9211 } },
			new ClientCall { { HealthyResponse, 9212 } },
			new ClientCall { { HealthyResponse, 9210 } }
		);
	}

	[Fact]
	public async Task DoesASniffAfterConnectionFailureOnPing()
	{

		var audit = new Auditor(() => Virtual.Elasticsearch.Bootstrap(5)
			.MasterEligible(9202, 9203, 9204)
			.Ping(r => r.OnPort(9201).Fails(Once))
			.Ping(r => r.SucceedAlways())
			.ClientCalls(c=>c.SucceedAlways())
			.Sniff(p => p.SucceedAlways(Virtual.Elasticsearch.Bootstrap(3)
				.MasterEligible(9200, 9202)
				.Ping(r => r.OnPort(9201).Fails(Once))
				.Ping(r => r.SucceedAlways())
				.ClientCalls(c=>c.SucceedAlways())
				.Sniff(s => s.SucceedAlways(Virtual.Elasticsearch.Bootstrap(3, 9210)
					.MasterEligible(9210, 9211)
					.Ping(r => r.SucceedAlways())
					.Sniff(r => r.SucceedAlways())
					.ClientCalls(c=>c.SucceedAlways())
				))
			))
			.SniffingNodePool()
			.Settings(s => s.SniffOnStartup(false))
		);

		audit = await audit.TraceCalls(
			new ClientCall {
				{ PingSuccess, 9200 },
				{ HealthyResponse, 9200 },
				{ pool =>  pool.Nodes.Count.Should().Be(5) }
			},
			new ClientCall {
				{ PingFailure, 9201},
				{ SniffOnFail },
				{ SniffSuccess, 9202},
				{ PingSuccess, 9200},
				{ HealthyResponse, 9200},
				{ pool =>  pool.Nodes.Count.Should().Be(3) }
			},
			new ClientCall {
				{ PingFailure, 9201},
				{ SniffOnFail },
				{ SniffSuccess, 9200},
				{ PingSuccess, 9210},
				{ HealthyResponse, 9210},
				{ pool =>  pool.Nodes.Count.Should().Be(3) }
			},
			new ClientCall {
				{ PingSuccess, 9211 },
				{ HealthyResponse, 9211 }
			},
			new ClientCall {
				{ PingSuccess, 9212 },
				{ HealthyResponse, 9212 }
			},
			new ClientCall { { HealthyResponse, 9210 } },
			new ClientCall { { HealthyResponse, 9211 } },
			new ClientCall { { HealthyResponse, 9212 } },
			new ClientCall { { HealthyResponse, 9210 } }
		);
	}

	[Fact]
	public async Task UsesPublishAddress()
	{
		var audit = new Auditor(() => Virtual.Elasticsearch.Bootstrap(2)
				.MasterEligible(9200)
				.ClientCalls(c=>c.SucceedAlways())
				.Ping(r => r.OnPort(9200).Fails(Once))
				.Ping(c=>c.SucceedAlways())
				.Sniff(p => p.SucceedAlways(Virtual.Elasticsearch.Bootstrap(10)
					.MasterEligible(9200, 9202, 9201)
					.PublishAddress("10.0.12.1")
					.ClientCalls(c=>c.SucceedAlways())
					.Ping(c=>c.SucceedAlways())
				))
				.SniffingNodePool()
				.Settings(s => s.SniffOnStartup(false))
		);

		void HostAssert(Diagnostics.Auditing.Audit a, string host, int expectedPort)
		{
			a.Node.Uri.Host.Should().Be(host);
			a.Node.Uri.Port.Should().Be(expectedPort);
		}
		void SniffUrlAssert(Diagnostics.Auditing.Audit a, string host, int expectedPort) => HostAssert(a, host, expectedPort);

		audit = await audit.TraceCalls(
			new ClientCall {
				{ PingFailure, a => HostAssert(a, "localhost", 9200)},
				{ SniffOnFail },
				{ SniffSuccess, a => SniffUrlAssert(a, "localhost", 9200)},
				{ PingSuccess, a => HostAssert(a, "10.0.12.1", 9200)},
				{ HealthyResponse,  a => HostAssert(a, "10.0.12.1", 9200)},
				{ pool =>  pool.Nodes.Count.Should().Be(10) }
			}
		);
	}
}
