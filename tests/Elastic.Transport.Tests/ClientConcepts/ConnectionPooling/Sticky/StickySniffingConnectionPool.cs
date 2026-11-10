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

namespace Elastic.Transport.Tests.ClientConcepts.ConnectionPooling.Sticky;

public class StickySniffingConnectionPool
{

	[Fact]
	public void EachViewStartsAtNextPositionAndWrapsOver()
	{
		var numberOfNodes = 10;
		var uris = Enumerable.Range(9200, numberOfNodes).Select(p => new Uri("http://localhost:" + p));
		var pool = new StickySniffingNodePool(uris, (n)=>0f);

		var startingPositions = Enumerable.Range(0, numberOfNodes)
			.Select(i => pool.CreateView(null).First())
			.Select(n => n.Uri.Port)
			.ToList();

		var expectedOrder = Enumerable.Repeat(9200, numberOfNodes);
		startingPositions.Should().Equal(expectedOrder);
	}

	[Fact]
	public async Task FavorsNodeWithGreatestWeightAndFallsOver()
	{
		IEnumerable<Node> Nodes(int start) => Enumerable.Range(start, 4)
			.Select(i => new Uri($"http://localhost:{9200 + i}"))
			.Select((u, i) => new Node(u)
			{
				Settings = new Dictionary<string, object> {{"rack", $"rack_{u.Port - 9200}"}}
			});

		var audit = new Auditor(() => Virtual.Elasticsearch.Bootstrap(Nodes(0))
			.ClientCalls(p => p.OnPort(9202).Succeeds(Twice))
			.ClientCalls(p => p.FailAlways())
			.Sniff(s=>s.SucceedAlways(Virtual.Elasticsearch.Bootstrap(Nodes(10))
				.ClientCalls(p => p.SucceedAlways()))
			)
			.StickySniffingNodePool(n=>
				(n.Settings.TryGetValue("rack", out var v) && v.ToString() == "rack_2" ? 10 : 0)
				+(n.Settings.TryGetValue("rack", out var r) && r.ToString() == "rack_11" ? 10 : 0)
			)
			.Settings(p => p.DisablePing().SniffOnStartup(false))
		);

		await audit.TraceCalls(
			new ClientCall { { HealthyResponse, 9202} },
			new ClientCall { { HealthyResponse, 9202} },
			new ClientCall {
				{ BadRequest, 9202 },
				{ SniffOnFail },
				{ SniffSuccess, 9200 },
				{ HealthyResponse, 9211},
			},

			new ClientCall { { HealthyResponse, 9211 } },
			new ClientCall { { HealthyResponse, 9211 } },
			new ClientCall { { HealthyResponse, 9211 } },
			new ClientCall { { HealthyResponse, 9211 } },
			new ClientCall { { HealthyResponse, 9211 } },
			new ClientCall { { HealthyResponse, 9211 } },
			new ClientCall { { HealthyResponse, 9211 } }
		);
	}
	[Fact]
	public async Task SniffOnStartupReseedsPutsMostWeightedNodeToFront()
	{
		IEnumerable<Node> Nodes(int start) => Enumerable.Range(start, 4)
			.Select(i => new Uri($"http://localhost:{9200 + i}"))
			.Select((u, i) => new Node(u)
			{
				Settings = new Dictionary<string, object> {{"rack", $"rack_{u.Port - 9200}"}}
			});

		var audit = new Auditor(() => Virtual.Elasticsearch.Bootstrap(4)
			.ClientCalls(p => p.SucceedAlways())
			.Sniff(s=>s.SucceedAlways(Virtual.Elasticsearch.Bootstrap(Nodes(0))
				.ClientCalls(p => p.SucceedAlways()))
			)
			.StickySniffingNodePool(n=>
				(n.Settings.TryGetValue("rack", out var v) && v.ToString() == "rack_2" ? 10 : 0)
			)
			.Settings(p => p.DisablePing())
		);

		await audit.TraceCalls(
			new ClientCall
			{
				{ SniffOnStartup },
				{ SniffSuccess, 9200 },
				{ HealthyResponse, 9202},
			},

			new ClientCall { { HealthyResponse, 9202} },
			new ClientCall { { HealthyResponse, 9202} },
			new ClientCall { { HealthyResponse, 9202} },
			new ClientCall { { HealthyResponse, 9202} },
			new ClientCall { { HealthyResponse, 9202} },
			new ClientCall { { HealthyResponse, 9202} },
			new ClientCall { { HealthyResponse, 9202} },
			new ClientCall { { HealthyResponse, 9202} }
		);
	}

	[Fact]
	public async Task PicksADifferentNodeEachTimeAnodeIsDown()
	{
		var audit = new Auditor(() => Virtual.Elasticsearch.Bootstrap(4)
			.ClientCalls(p => p.Fails(Always))
			.StickySniffingNodePool()
			.Settings(p => p.DisablePing().SniffOnStartup(false).SniffOnConnectionFault(false))
		);

		await audit.TraceCalls(

			new ClientCall {
				{ BadRequest, 9200},
				{ BadRequest, 9201},
				{ BadRequest, 9202},
				{ BadRequest, 9203},
				{ MaxRetriesReached },
				{ FailedOverAllNodes },
				{ pool => pool.Nodes.Where(n=>!n.IsAlive).Should().HaveCount(4) }
			},

			new ClientCall {
				{ AllNodesDead },
				{ Resurrection, 9200},
				{ BadRequest, 9200},
				{ pool =>  pool.Nodes.Where(n=>!n.IsAlive).Should().HaveCount(4) }
			},
			new ClientCall {
				{ AllNodesDead },
				{ Resurrection, 9201},
				{ BadRequest, 9201},
				{ pool =>  pool.Nodes.Where(n=>!n.IsAlive).Should().HaveCount(4) }
			},
			new ClientCall {
				{ AllNodesDead },
				{ Resurrection, 9202},
				{ BadRequest, 9202},
				{ pool =>  pool.Nodes.Where(n=>!n.IsAlive).Should().HaveCount(4) }
			},
			new ClientCall {
				{ AllNodesDead },
				{ Resurrection, 9203},
				{ BadRequest, 9203},
				{ pool =>  pool.Nodes.Where(n=>!n.IsAlive).Should().HaveCount(4) }
			}
		);
	}

}
