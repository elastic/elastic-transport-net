// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace Elastic.Transport.Tests.ClientConcepts.ConnectionPooling.BuildingBlocks;

public class NodePools
{
	private static IEnumerable<Uri> Uris(int count = 5) =>
		Enumerable.Range(9200, count).Select(port => new Uri($"http://localhost:{port}"));

	[Fact]
	public void SingleNode()
	{
		var uri = new Uri("http://localhost:9201");
		var pool = new SingleNodePool(uri);
		var config = new TransportConfiguration(pool);

		pool.Nodes.Should().HaveCount(1);
		pool.Nodes.First().Uri.Port.Should().Be(9201);
		pool.SupportsReseeding.Should().BeFalse();
		pool.SupportsPinging.Should().BeFalse();
		config.NodePool.Should().BeOfType<SingleNodePool>();

		new TransportConfiguration(uri).NodePool.Should().BeOfType<SingleNodePool>();
	}

	[Fact]
	public void Static()
	{
		var pool = new StaticNodePool(Uris());
		pool = new StaticNodePool(Uris().Select(u => new Node(u)));

		pool.Nodes.Should().HaveCount(5);
		pool.SupportsReseeding.Should().BeFalse();
		pool.SupportsPinging.Should().BeTrue();
		new TransportConfiguration(pool).NodePool.Should().BeOfType<StaticNodePool>();
	}

	private sealed class SeededRandomNodePool(IEnumerable<Node> nodes, int seed) : StaticNodePool(nodes, randomize: true, randomizeSeed: seed);

	[Fact]
	public void RandomizedInitialNodes()
	{
		const int nodeCount = 100;
		var nodes = Enumerable.Range(1, nodeCount)
			.Select(i => new Node(new Uri($"https://10.0.0.{i}:9200/")))
			.ToList();

		var pools = Enumerable.Range(0, nodeCount).Select(i => new SeededRandomNodePool(nodes, 42 + i)).ToList();

		pools.Should().HaveCount(nodeCount);
		pools
			.Select(p => p.CreateView(null).First().Uri.ToString())
			.All(uri => uri == "https://10.0.0.1:9200/")
			.Should()
			.BeFalse();
	}

	[Fact]
	public void Sniffing()
	{
		var pool = new SniffingNodePool(Uris());
		pool = new SniffingNodePool(Uris().Select(u => new Node(u)));

		pool.Nodes.Should().HaveCount(5);
		pool.SupportsReseeding.Should().BeTrue();
		pool.SupportsPinging.Should().BeTrue();
		new TransportConfiguration(pool).NodePool.Should().BeOfType<SniffingNodePool>();
	}

	[Fact]
	public void Sticky()
	{
		var pool = new StickyNodePool(Uris());
		pool = new StickyNodePool(Uris().Select(u => new Node(u)));

		pool.Nodes.Should().HaveCount(5);
		pool.SupportsReseeding.Should().BeFalse();
		pool.SupportsPinging.Should().BeTrue();
		new TransportConfiguration(pool).NodePool.Should().BeOfType<StickyNodePool>();
	}

	[Fact]
	public void SniffingSortedSticky()
	{
		var pool = new StickySniffingNodePool(Uris(), node =>
		{
			var weight = 0f;
			if (node.Settings.TryGetValue("node.attr.rack_id", out var rackId) && rackId.ToString() == "rack_one")
				weight += 10;
			return weight;
		});

		pool.Nodes.Should().HaveCount(5);
		pool.SupportsReseeding.Should().BeTrue();
		pool.SupportsPinging.Should().BeTrue();
		new TransportConfiguration(pool).NodePool.Should().BeOfType<StickySniffingNodePool>();
	}
}
