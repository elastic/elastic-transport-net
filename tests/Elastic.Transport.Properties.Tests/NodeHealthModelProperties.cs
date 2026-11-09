// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Linq;
using CsCheck;
using FluentAssertions;
using Xunit;

namespace Elastic.Transport.Properties.Tests;

/// <summary>A node, driven through any sequence of operations, behaves like a small reference model after every step.</summary>
public class NodeHealthModelProperties
{
	private enum Op { MarkDead, MarkAlive, Resurrect }

	private enum State { Untested, Alive, Dead, Resurrected }

	/// <summary>The four states written as a table, independently of how Node implements them.</summary>
	private sealed record Model(State State, int Failed, DateTimeOffset Until)
	{
		public static readonly Model Start = new(State.Untested, 0, default);

		public bool IsAlive => State is State.Untested or State.Alive;

		public bool IsResurrected => State is State.Untested or State.Resurrected;

		public Model Apply(Op op, DateTimeOffset until) => op switch
		{
			Op.MarkDead => new Model(State.Dead, Failed + 1, until),
			Op.MarkAlive => new Model(State.Alive, 0, default),
			Op.Resurrect => State switch
			{
				State.Alive => new Model(State.Untested, 0, default),
				State.Dead => this with { State = State.Resurrected },
				_ => this,
			},
			_ => throw new ArgumentOutOfRangeException(nameof(op)),
		};
	}

	[Fact]
	public void ANodeMatchesTheModelAfterEveryOperation() =>
		Gen.Enum<Op>().Array[0, 40].Sample(ops =>
		{
			var node = new Node(new Uri("http://localhost:9200"));
			var model = Model.Start;
			var history = new System.Collections.Generic.List<string>();

			for (var i = 0; i < ops.Length; i++)
			{
				var until = DateTimeOffset.UnixEpoch.AddMinutes(i + 1);
				history.Add(ops[i].ToString());
				switch (ops[i])
				{
					case Op.MarkDead: node.MarkDead(until); break;
					case Op.MarkAlive: node.MarkAlive(); break;
					case Op.Resurrect: node.Resurrect(); break;
				}
				model = model.Apply(ops[i], until);

				var because = string.Join(" > ", history);
				_ = node.IsAlive.Should().Be(model.IsAlive, because);
				_ = node.IsResurrected.Should().Be(model.IsResurrected, because);
				_ = node.FailedAttempts.Should().Be(model.Failed, because);
				_ = node.DeadUntil.Should().Be(model.Until, because);
			}
		}, iter: 1000);

	[Fact]
	public void ACloneIsAnIndependentNodeInTheSameState() =>
		Gen.Enum<Op>().Array[0, 20].Sample(ops =>
		{
			var node = new Node(new Uri("http://localhost:9200"));
			for (var i = 0; i < ops.Length; i++)
				Apply(node, ops[i], i);

			var clone = node.Clone();

			_ = (clone.IsAlive, clone.IsResurrected, clone.FailedAttempts, clone.DeadUntil)
				.Should().Be((node.IsAlive, node.IsResurrected, node.FailedAttempts, node.DeadUntil));

			clone.MarkDead(DateTimeOffset.UnixEpoch.AddYears(1));
			_ = node.FailedAttempts.Should().Be(ops.Count(o => o == Op.MarkDead) - LastResetCount(ops), "changing the clone must not change the original");
		}, iter: 300);

	private static void Apply(Node node, Op op, int i)
	{
		switch (op)
		{
			case Op.MarkDead: node.MarkDead(DateTimeOffset.UnixEpoch.AddMinutes(i + 1)); break;
			case Op.MarkAlive: node.MarkAlive(); break;
			case Op.Resurrect: node.Resurrect(); break;
		}
	}

	/// <summary>MarkDead calls that a later MarkAlive wiped out.</summary>
	private static int LastResetCount(Op[] ops)
	{
		var lastAlive = Array.LastIndexOf(ops, Op.MarkAlive);
		return lastAlive < 0 ? 0 : ops.Take(lastAlive + 1).Count(o => o == Op.MarkDead);
	}
}
