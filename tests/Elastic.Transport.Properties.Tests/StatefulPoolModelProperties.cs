// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.Linq;
using CsCheck;
using Elastic.Transport.Diagnostics.Auditing;
using Elastic.Transport.VirtualizedCluster;
using Elastic.Transport.VirtualizedCluster.Components;
using FluentAssertions;
using Xunit;

namespace Elastic.Transport.Properties.Tests;

/// <summary>
/// A node pool over time: requests, clock jumps and nodes going down and coming back, in any order. A reference model of the
/// pool (round-robin over the usable nodes, a failed node backs off and returns when its time is up, a request has a retry
/// budget) is run beside the real transport, and they must agree after every step: which nodes were tried, in what order,
/// whether the request succeeded, and the state of every node. Single-request tests cannot see "it came back after the wait".
/// </summary>
public class StatefulPoolModelProperties
{
	private enum Kind { Request, Advance, Outage }

	private sealed record Op(Kind Kind, int Arg, bool Up)
	{
		public override string ToString() => Kind switch
		{
			Kind.Request => "request",
			Kind.Advance => $"+{Arg}s",
			_ => $"node{Arg} {(Up ? "up" : "down")}",
		};
	}

	private sealed record Scenario(int Nodes, int? MaxRetries, Op[] Ops)
	{
		public override string ToString() => $"nodes={Nodes} maxRetries={MaxRetries?.ToString() ?? "default"} ops=[{string.Join(", ", Ops.Select(o => o.ToString()))}]";

		public static readonly Gen<Scenario> Generator =
			from nodes in Gen.Int[2, 6]
			from maxRetries in Gen.Int[-1, 6]
			from ops in Gen.Frequency(
				(5, Gen.Const(Kind.Request).Select(k => new Op(k, 0, true))),
				(2, Gen.Int[1, 4000].Select(s => new Op(Kind.Advance, s, true))),
				(3, Gen.Select(Gen.Int[0, nodes - 1], Gen.Bool).Select(t => new Op(Kind.Outage, t.Item1, t.Item2)))).Array[1, 40]
			select new Scenario(nodes, maxRetries < 0 ? null : maxRetries, ops);
	}

	private sealed class ModelNode
	{
		public bool Alive = true;
		public int Failed;
		public DateTimeOffset Until;
	}

	/// <summary>The documented behaviour of a static pool, with none of its code.</summary>
	private sealed class Model(int nodes, int? maxRetries)
	{
		private readonly ModelNode[] _nodes = Enumerable.Range(0, nodes).Select(_ => new ModelNode()).ToArray();
		private int _cursor = -1;

		public ModelNode this[int i] => _nodes[i];

		public (List<int> Tried, bool Success) Request(DateTimeOffset now, bool[] healthy)
		{
			_cursor++;

			// Usable: alive, or dead with its time up (such a node is offered again, and pinged first if pinging were on).
			var usable = Enumerable.Range(0, nodes).Where(i => _nodes[i].Alive || _nodes[i].Until <= now).ToList();

			// Offered in turn, starting one further along than the previous request. When none is usable, one is tried anyway.
			var view = usable.Count == 0
				? [_cursor % nodes]
				: usable.Skip(_cursor % usable.Count).Concat(usable.Take(_cursor % usable.Count)).ToList();

			var attempts = Math.Min(maxRetries ?? int.MaxValue, nodes - 1) + 1;
			var tried = new List<int>();
			foreach (var i in view)
			{
				if (tried.Count >= attempts)
					break;
				tried.Add(i);

				if (healthy[i])
				{
					_nodes[i].Alive = true;
					_nodes[i].Failed = 0;
					_nodes[i].Until = default;
					return (tried, true);
				}

				// A failure takes the node out until now + a wait that grows with each failure in a row (60 s base, 30 min ceiling).
				var wait = Math.Min(60_000d * 2 * Math.Pow(2, (_nodes[i].Failed * 0.5) - 1), 30 * 60_000d);
				_nodes[i].Until = now.AddMilliseconds(wait);
				_nodes[i].Failed++;
				_nodes[i].Alive = false;
			}

			return (tried, false);
		}
	}

	[Fact]
	public void ThePoolBehavesLikeItsModelOverAnyScheduleOfRequestsOutagesAndTime() =>
		Scenario.Generator.Sample(scenario =>
		{
			VirtualCluster cluster = Virtual.Elasticsearch.Bootstrap(scenario.Nodes);
			var healthy = Enumerable.Repeat(true, scenario.Nodes).ToArray();

			void ApplyHealth()
			{
				cluster.ClientCallRules.Clear();
				for (var i = 0; i < scenario.Nodes; i++)
				{
					var port = 9200 + i;
					var up = healthy[i];
					_ = cluster.ClientCalls(r => up ? r.OnPort(port).SucceedAlways() : r.OnPort(port).FailAlways());
				}
			}
			ApplyHealth();

			var virtualized = cluster.StaticNodePool().Settings(s =>
			{
				s = s.DisablePing().ThrowExceptions(false);
				return scenario.MaxRetries is { } m ? s.MaximumRetries(m) : s;
			});
			var model = new Model(scenario.Nodes, scenario.MaxRetries);
			var nodes = virtualized.ConnectionPool.Nodes.ToArray();
			var history = new List<string>();

			foreach (var op in scenario.Ops)
			{
				history.Add(op.ToString());
				var because = $"{scenario}\nafter: {string.Join(" > ", history)}";

				switch (op.Kind)
				{
					case Kind.Advance:
						virtualized.ChangeTime(t => t.AddSeconds(op.Arg));
						break;

					case Kind.Outage:
						healthy[op.Arg] = op.Up;
						ApplyHealth();
						break;

					case Kind.Request:
						var now = virtualized.ConnectionPool.DateTimeProvider.Now();
						var (expectedTried, expectedSuccess) = model.Request(now, healthy);

						var response = virtualized.ClientCall();
						var tried = response.ApiCallDetails.AuditTrail!
							.Where(a => a.Event is AuditEvent.HealthyResponse or AuditEvent.BadRequest or AuditEvent.BadResponse)
							.Select(a => a.Node!.Uri.Port - 9200)
							.ToList();

						_ = tried.Should().Equal(expectedTried, "the nodes tried, in order. " + because);
						_ = response.ApiCallDetails.HasSuccessfulStatusCode.Should().Be(expectedSuccess, because);
						break;
				}

				for (var i = 0; i < scenario.Nodes; i++)
				{
					_ = nodes[i].IsAlive.Should().Be(model[i].Alive, $"node {i} alive. {because}");
					_ = nodes[i].FailedAttempts.Should().Be(model[i].Failed, $"node {i} failures. {because}");
					if (!model[i].Alive)
						_ = nodes[i].DeadUntil.Should().Be(model[i].Until, $"node {i} back at. {because}");
				}
			}
		}, iter: 300);
}
