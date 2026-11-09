// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.Linq;
using CsCheck;
using FluentAssertions;
using Xunit;

namespace Elastic.Transport.Properties.Tests;

/// <summary>
/// Two (or three) requests walking nodes on ONE shared pipeline, their steps interleaved in any order, no threads: each
/// request's walk must be exactly what it would have been if it ran alone. The pipeline is shared, so any state that
/// leaks into it (a reseed flag, an attempt count) shows up here deterministically, where a concurrent test can only
/// hope to hit the interleaving.
/// </summary>
public class PipelineStateIsolationProperties
{
	private const int Nodes = 4;

	private enum Step { Next, Reseed }

	/// <summary>The walk of a request that runs alone, written as a small state machine from the documented behaviour.</summary>
	private sealed class Reference
	{
		private bool _hasView, _refresh, _done, _lastWasNode;
		private int _remaining;

		public int Views { get; private set; }

		public int Attempted { get; private set; }

		public bool? Next()
		{
			if (_done)
				return null;
			_lastWasNode = false;

			if (_hasView && _refresh)
			{
				_refresh = false;
				_hasView = false;
			}
			if (!_hasView)
			{
				if (Views >= 100 || Attempted >= Nodes)
				{
					_done = true;
					return false;
				}
				Views++;
				_hasView = true;
				_remaining = Nodes;
			}
			if (_remaining == 0 || Attempted >= Nodes)
			{
				_hasView = false;
				_done = true;
				return false;
			}
			_remaining--;
			Attempted++;
			_lastWasNode = true;
			return true;
		}

		/// <summary>A sniff during a node's turn: only possible right after a node was offered.</summary>
		public bool Reseed()
		{
			if (!_lastWasNode || _done)
				return false;
			_refresh = true;
			return true;
		}
	}

	[Fact]
	public void EachRequestsWalkIsWhatItWouldBeAloneWhateverTheInterleaving() =>
		Gen.Select(Gen.Int[2, 3], Gen.Select(Gen.Int[0, 2], Gen.Enum<Step>()).Array[0, 60]).Sample((requests, schedule) =>
		{
			var pool = new StaticNodePool(Enumerable.Range(0, Nodes).Select(i => new Node(new Uri($"http://n{i}:9200"))), randomize: false);
			using var settings = new TransportConfiguration(pool, new InMemoryRequestInvoker());
			var pipeline = DefaultRequestPipelineFactory.Default.Create(new BoundConfiguration(settings));
			var started = DateTimeOffset.UtcNow;

			var real = Enumerable.Range(0, requests).Select(_ => new RequestAttempt()).ToArray();
			var reference = Enumerable.Range(0, requests).Select(_ => new Reference()).ToArray();
			var lastReal = new bool[requests];
			var trace = new List<string>();

			foreach (var (who, step) in schedule)
			{
				var id = who % requests;
				trace.Add($"{id}:{step}");
				var because = string.Join(" ", trace);

				if (step == Step.Next)
				{
					var expected = reference[id].Next();
					if (expected is null)
						continue;

					var got = pipeline.MoveNext(real[id], started, null);
					if (got)
						real[id].AttemptedNodes++;
					lastReal[id] = got;
					_ = got.Should().Be(expected.Value, because);
				}
				else if (reference[id].Reseed())
				{
					// What a sniff during this request's own turn does: ask this request, and only this request, to restart its walk.
					real[id].RefreshRequested = true;
				}

				for (var i = 0; i < requests; i++)
				{
					_ = real[i].Views.Should().Be(reference[i].Views, $"request {i} after: {because}");
					_ = real[i].AttemptedNodes.Should().Be(reference[i].Attempted, $"request {i} after: {because}");
				}
			}
		}, iter: 1000);
}
