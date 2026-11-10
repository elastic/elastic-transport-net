// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Linq;
using CsCheck;
using FluentAssertions;
using Xunit;

namespace Elastic.Transport.Properties.Tests;

/// <summary>What a pool offers a request, for any mix of alive, dead and due-for-a-retry nodes.</summary>
public class NodePoolViewProperties
{
	private enum Health { Alive, DeadForAnHour, DueForRetry }

	private static (StaticNodePool Pool, Node[] Nodes) Build(Health[] health)
	{
		var nodes = health.Select((_, i) => new Node(new Uri($"http://n{i}:9200"))).ToArray();
		var pool = new StaticNodePool(nodes, randomize: false);
		for (var i = 0; i < health.Length; i++)
		{
			nodes[i].MarkAlive(); // a clean slate: not "untested"
			switch (health[i])
			{
				case Health.DeadForAnHour: nodes[i].MarkDead(DateTimeOffset.UtcNow.AddHours(1)); break;
				case Health.DueForRetry: nodes[i].MarkDead(DateTimeOffset.UtcNow.AddHours(-1)); break;
			}
		}
		return (pool, nodes);
	}

	private static readonly Gen<Health[]> Mixes = Gen.Enum<Health>().Array[1, 8];

	[Fact]
	public void AViewOffersEveryUsableNodeExactlyOnce() =>
		Mixes.Sample(health =>
		{
			var (pool, nodes) = Build(health);
			var usable = nodes.Where((_, i) => health[i] != Health.DeadForAnHour).Select(n => n.Uri).ToHashSet();

			var view = pool.CreateView().ToList();

			if (usable.Count > 0)
			{
				_ = view.Select(n => n.Uri).Should().BeEquivalentTo(usable, "a usable node is alive, or dead with its time up");
				_ = view.Select(n => n.Uri).Distinct().Count().Should().Be(view.Count, "no node twice in one view");
			}
			else
			{
				_ = view.Should().HaveCount(1, "with every node dead, one is offered to try");
				_ = view[0].IsResurrected.Should().BeTrue("a node tried while everything is dead is due a ping");
			}
		}, iter: 500);

	[Fact]
	public void ConsecutiveViewsRotateByOneSoLoadIsSpread() =>
		Gen.Int[2, 8].Sample(count =>
		{
			var (pool, _) = Build(Enumerable.Repeat(Health.Alive, count).ToArray());

			var first = pool.CreateView().Select(n => n.Uri).ToList();
			var second = pool.CreateView().Select(n => n.Uri).ToList();

			_ = second.Should().Equal(first.Skip(1).Concat(first.Take(1)), "each view starts one node further on");
		}, iter: 100);

	[Fact]
	public void ADeadNodeIsNeverOfferedWhileAnotherIsUsable() =>
		Mixes.Where(h => h.Any(x => x == Health.Alive) && h.Any(x => x == Health.DeadForAnHour)).Sample(health =>
		{
			var (pool, nodes) = Build(health);
			var dead = nodes.Where((_, i) => health[i] == Health.DeadForAnHour).Select(n => n.Uri).ToHashSet();

			for (var i = 0; i < 5; i++)
				_ = pool.CreateView().Select(n => n.Uri).Should().NotIntersectWith(dead);
		}, iter: 300);

	[Fact]
	public void ADueNodeIsMarkedForAPingBeforeItIsOffered() =>
		Mixes.Where(h => h.Any(x => x == Health.DueForRetry)).Sample(health =>
		{
			var (pool, nodes) = Build(health);

			var offered = pool.CreateView().ToList();

			foreach (var (node, i) in nodes.Select((n, i) => (n, i)).Where(t => health[t.i] == Health.DueForRetry))
				_ = offered.Should().Contain(node).Which.IsResurrected.Should().BeTrue($"node {i} was dead and is being tried again");
		}, iter: 300);
}
