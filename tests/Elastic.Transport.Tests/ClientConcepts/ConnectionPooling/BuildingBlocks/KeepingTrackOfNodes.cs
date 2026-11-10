// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using Elastic.Transport.Products.Elasticsearch;
using FluentAssertions;
using Xunit;

namespace Elastic.Transport.Tests.ClientConcepts.ConnectionPooling.BuildingBlocks;

public class KeepingTrackOfNodes
{
	[Fact]
	public void Creating()
	{
		var node = new Node(new Uri("http://localhost:9200"));

		node.Uri.Should().NotBeNull();
		node.Uri.Port.Should().Be(9200);
		node.HasFeature(ElasticsearchNodeFeatures.MasterEligible).Should().BeTrue();
		node.HasFeature(ElasticsearchNodeFeatures.HoldsData).Should().BeTrue();
		node.IsResurrected.Should().BeTrue();
	}

	[Fact]
	public void BuildingPaths()
	{
		var node = new Node(new Uri("http://test.example/elasticsearch"));
		node.Uri.Port.Should().Be(80);
		node.Uri.AbsolutePath.Should().Be("/elasticsearch/");

		var combinedPath = new Uri(node.Uri, "index/type/_search");
		combinedPath.AbsolutePath.Should().Be("/elasticsearch/index/type/_search");

		combinedPath = node.CreatePath("index/type/_search");
		combinedPath.AbsolutePath.Should().Be("/elasticsearch/index/type/_search");
	}

	[Fact]
	public void MarkNodes()
	{
		var node = new Node(new Uri("http://localhost:9200"));
		node.FailedAttempts.Should().Be(0);
		node.IsAlive.Should().BeTrue();

		for (var i = 0; i < 10; i++)
		{
			var deadUntil = DateTimeOffset.Now.AddMinutes(1);
			node.MarkDead(deadUntil);
			node.FailedAttempts.Should().Be(i + 1);
			node.IsAlive.Should().BeFalse();
			node.DeadUntil.Should().Be(deadUntil);
		}

		node.MarkAlive();
		node.FailedAttempts.Should().Be(0);
		node.DeadUntil.Should().Be(default);
		node.IsAlive.Should().BeTrue();
	}

	[Fact]
	public void Equality()
	{
		var node = new Node(new Uri("http://localhost:9200"), ElasticsearchNodeFeatures.Default);
		var nodeWithOtherFeatures = new Node(new Uri("http://localhost:9200"), [ElasticsearchNodeFeatures.HoldsData]);

		(node == nodeWithOtherFeatures).Should().BeTrue();
		(node != nodeWithOtherFeatures).Should().BeFalse();

		var uri = new Uri("http://localhost:9200");
		(node == uri).Should().BeTrue();

		var differentUri = new Uri("http://localhost:9201");
		(node != differentUri).Should().BeTrue();

		node.Should().Be(nodeWithOtherFeatures);
	}
}
