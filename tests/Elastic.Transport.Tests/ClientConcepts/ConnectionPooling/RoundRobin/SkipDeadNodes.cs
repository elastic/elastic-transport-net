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

namespace Elastic.Transport.Tests.ClientConcepts.ConnectionPooling.RoundRobin;

public class SkippingDeadNodes
{

	private const int NumberOfNodes = 3;

	[Fact]
	public void EachViewSkipsAheadWithOne()
	{
		var seeds = Enumerable.Range(9200, NumberOfNodes).Select(p => new Node(new Uri("http://localhost:" + p))).ToList();
		var pool = new StaticNodePool(seeds, randomize: false);
		for (var i = 0; i < 20; i++)
		{
			var node = pool.CreateView(null).First();
			node.Uri.Port.Should().Be(9200);
			node = pool.CreateView(null).First();
			node.Uri.Port.Should().Be(9201);
			node = pool.CreateView(null).First();
			node.Uri.Port.Should().Be(9202);
		}
	}

	[Fact]
	public void EachViewSeesNextButSkipsTheDeadNode()
	{
		var seeds = Enumerable.Range(9200, NumberOfNodes).Select(p => new Node(new Uri("http://localhost:" + p))).ToList();
		seeds.First().MarkDead(DateTimeOffset.Now.AddDays(1));
		var pool = new StaticNodePool(seeds, randomize: false);
		for (var i = 0; i < 20; i++)
		{
			var node = pool.CreateView(null).First();
			node.Uri.Port.Should().Be(9201);
			node = pool.CreateView(null).First();
			node.Uri.Port.Should().Be(9202);
		}

		seeds.First().MarkAlive();
		for (var i = 0; i < 20; i++)
		{
			var node = pool.CreateView(null).First();
			node.Uri.Port.Should().Be(9201);
			node = pool.CreateView(null).First();
			node.Uri.Port.Should().Be(9202);
			node = pool.CreateView(null).First();
			node.Uri.Port.Should().Be(9200);
		}
	}
	[Fact]
	public void ViewSeesResurrectedNodes()
	{
		var dateTimeProvider = new TestableDateTimeProvider();
		var seeds = Enumerable.Range(9200, NumberOfNodes).Select(p => new Node(new Uri("http://localhost:" + p))).ToList();
		seeds.First().MarkDead(dateTimeProvider.Now().AddDays(1));
		var pool = new StaticNodePool(seeds, randomize: false) { DateTimeProvider = dateTimeProvider };
		for (var i = 0; i < 20; i++)
		{
			var node = pool.CreateView(null).First();
			node.Uri.Port.Should().Be(9201);
			node = pool.CreateView(null).First();
			node.Uri.Port.Should().Be(9202);
		}

		dateTimeProvider.ChangeTime(d => d.AddDays(2));
		var n = pool.CreateView(null).First();
		n.Uri.Port.Should().Be(9201);
		n = pool.CreateView(null).First();
		n.Uri.Port.Should().Be(9202);
		n = pool.CreateView(null).First();
		n.Uri.Port.Should().Be(9200);
		n.IsResurrected.Should().BeTrue();
	}

	[Fact]
	public async Task FallsOverDeadNodes()
	{

		var audit = new Auditor(() => Virtual.Elasticsearch.Bootstrap(4)
			.ClientCalls(p => p.Succeeds(Always))
			.ClientCalls(p => p.OnPort(9201).FailAlways())
			.ClientCalls(p => p.OnPort(9203).FailAlways())
			.StaticNodePool()
			.Settings(p=>p.DisablePing())
		);

		await audit.TraceCalls(
			new ClientCall {
				{ HealthyResponse, 9200},
				{ pool => pool.Nodes.Where(n=>!n.IsAlive).Should().HaveCount(0) }
			},
			new ClientCall {
				{ BadRequest, 9201},
				{ HealthyResponse, 9202},

				{ pool =>  pool.Nodes.Where(n=>!n.IsAlive).Should().HaveCount(1) }
			},
			new ClientCall {
				{ BadRequest, 9203},
				{ HealthyResponse, 9200},
				{ pool => pool.Nodes.Where(n=>!n.IsAlive).Should().HaveCount(2) }
			},
			new ClientCall {
				{ HealthyResponse, 9202},
				{ pool => pool.Nodes.Where(n=>!n.IsAlive).Should().HaveCount(2) }
			},
			new ClientCall {
				{ HealthyResponse, 9200},
				{ pool => pool.Nodes.Where(n=>!n.IsAlive).Should().HaveCount(2) }
			},
			new ClientCall {
				{ HealthyResponse, 9202},
				{ pool => pool.Nodes.Where(n=>!n.IsAlive).Should().HaveCount(2) }
			},
			new ClientCall {
				{ HealthyResponse, 9200},
				{ pool => pool.Nodes.Where(n=>!n.IsAlive).Should().HaveCount(2) }
			}
		);
	}

	[Fact]
	public async Task PicksADifferentNodeEachTimeAnodeIsDown()
	{

		var audit = new Auditor(() => Virtual.Elasticsearch.Bootstrap(4)
			.ClientCalls(p => p.Fails(Always))
			.StaticNodePool()
			.Settings(p=>p.DisablePing())
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
			},
			new ClientCall {
				{ AllNodesDead },
				{ Resurrection, 9200},
				{ BadRequest, 9200},
				{ pool =>  pool.Nodes.Where(n=>!n.IsAlive).Should().HaveCount(4) }
			}
		);
	}
}
