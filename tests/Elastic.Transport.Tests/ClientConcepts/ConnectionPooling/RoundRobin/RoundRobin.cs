// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Concurrent;
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

public class RoundRobin
{

	private const int NumberOfNodes = 10;

	[Fact]
	public void EachViewStartsAtNexPositionAndWrapsOver()
	{
		var uris = Enumerable.Range(9200, NumberOfNodes).Select(p => new Uri("http://localhost:" + p));
		var staticPool = new StaticNodePool(uris, randomize: false);
		var sniffingPool = new SniffingNodePool(uris, randomize: false);

		this.AssertCreateView(staticPool);
		this.AssertCreateView(sniffingPool);
	}

	private void AssertCreateView(NodePool pool)
	{

		var startingPositions = Enumerable.Range(0, NumberOfNodes)
			.Select(i => pool.CreateView(null).First())
			.Select(n => n.Uri.Port)
			.ToList();

		var expectedOrder = Enumerable.Range(9200, NumberOfNodes);
		startingPositions.Should().Equal(expectedOrder);

		var threadedStartPositions = new ConcurrentBag<int>();
		var threads = Enumerable.Range(0, 20)
			.Select(i => CreateThreadCallingCreateView(pool, threadedStartPositions))
			.ToList();

		foreach (var t in threads) t.Start();
		foreach (var t in threads) t.Join();

		var grouped = threadedStartPositions.GroupBy(p => p).ToList();
		grouped.Count.Should().Be(NumberOfNodes);
		grouped.Select(p => p.Count()).Should().OnlyContain(p => p == 2);
	}

	public Thread CreateThreadCallingCreateView(NodePool pool, ConcurrentBag<int> startingPositions) => new Thread(() =>
	{

		var seenPorts = CallCreateView(pool).Take(NumberOfNodes * 10).ToList();
		var startPosition = seenPorts.First();
		startingPositions.Add(startPosition);
		var i = (startPosition - 9200) % NumberOfNodes;
		foreach (var port in seenPorts)
			port.Should().Be(9200 + (i++ % NumberOfNodes));
	});

	private IEnumerable<int> CallCreateView(NodePool pool)
	{
		foreach(var n in pool.CreateView(null)) yield return n.Uri.Port;
	}
}
