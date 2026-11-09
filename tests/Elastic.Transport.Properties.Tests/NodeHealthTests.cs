// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Linq;
using System.Threading.Tasks;
using CsCheck;
using FluentAssertions;
using Nullean.Entities;
using Nullean.Entities.CsCheck;
using Xunit;

namespace Elastic.Transport.Properties.Tests;

/// <summary>What a node looks like from the outside after some history.</summary>
public sealed record NodeSnapshot(bool IsAlive, bool IsResurrected, int FailedAttempts, DateTimeOffset DeadUntil, int MarkDeadCalls)
{
	public enum Op { MarkDead, MarkAlive, Resurrect }

	public static NodeSnapshot After(Op[] history)
	{
		var node = new Node(new Uri("http://localhost:9200"));
		var deadCalls = 0;
		foreach (var op in history)
		{
			switch (op)
			{
				case Op.MarkDead: node.MarkDead(DateTimeOffset.UnixEpoch.AddMinutes(1)); deadCalls++; break;
				case Op.MarkAlive: node.MarkAlive(); deadCalls = 0; break;
				case Op.Resurrect: node.Resurrect(); break;
			}
		}
		return new NodeSnapshot(node.IsAlive, node.IsResurrected, node.FailedAttempts, node.DeadUntil, deadCalls);
	}

	public static readonly Gen<NodeSnapshot> Generator =
		Gen.Enum<Op>().Array[0, 30].Select(After);
}

/// <summary>The combinations of a node's flags that may ever be observed, whatever happened to it.</summary>
[Constraints<NodeSnapshot>]
public static partial class NodeSnapshotConstraints
{
	public static readonly Constraint<NodeSnapshot> AliveNodesHaveNoFailures = Constraints<NodeSnapshot>.Always(
		n => !n.IsAlive || (n.FailedAttempts == 0 && n.DeadUntil == default),
		"A node that is alive has no failed attempts and no dead-until time.");

	public static readonly Constraint<NodeSnapshot> DeadNodesHaveFailedAndHaveADeadline = Constraints<NodeSnapshot>.Always(
		n => n.IsAlive || (n.FailedAttempts >= 1 && n.DeadUntil != default),
		"A node that is not alive has failed at least once and says until when.");

	public static readonly Constraint<NodeSnapshot> FailedAttemptsCountConsecutiveFailures = Constraints<NodeSnapshot>.Always(
		n => n.FailedAttempts == n.MarkDeadCalls,
		"FailedAttempts is the number of MarkDead calls since the last MarkAlive, none lost.");
}

public class NodeHealthTests
{
	[Fact]
	public async Task NodesOnlyShowStatesThatExist() =>
		await AutoTest.For(NodeSnapshotConstraints.Instance, NodeSnapshot.Generator).RunAsync();

	/// <summary>Two threads failing one node must both count: this was <c>FailedAttempts++</c> on an auto-property.</summary>
	[Fact]
	public void ConcurrentFailuresAreAllCounted()
	{
		const int threads = 8, each = 5_000;
		var node = new Node(new Uri("http://localhost:9200"));
		var until = DateTimeOffset.UnixEpoch.AddMinutes(1);

		_ = Parallel.For(0, threads, _ =>
		{
			for (var i = 0; i < each; i++)
				node.MarkDead(until);
		});

		_ = node.FailedAttempts.Should().Be(threads * each);
	}
}
