// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using CsCheck;
using Elastic.Transport.Diagnostics.Auditing;
using Elastic.Transport.VirtualizedCluster;
using Elastic.Transport.VirtualizedCluster.Components;
using FluentAssertions;
using Xunit;

namespace Elastic.Transport.Properties.Tests;

/// <summary>
/// Many requests share one transport over a sniffing pool, failing nodes and reseeding the pool underneath each other.
/// Whatever the others do, a request's own trail must still obey the rules of a single request. This is what the
/// per-request <see cref="RequestAttempt"/> is for: its attempt count and "the pool was reseeded" flag used to live on
/// the shared pipeline, so one request could consume another's reseed signal and be sent back over nodes it had tried.
/// </summary>
public class ConcurrentSniffingPoolTests
{
	private sealed record Case(bool[] Healthy, int MaxRetries, int Parallelism)
	{
		public int Nodes => Healthy.Length;

		public override string ToString() =>
			$"nodes=[{string.Join("", Healthy.Select(h => h ? 'H' : 'x'))}] maxRetries={MaxRetries} parallelism={Parallelism}";

		public static readonly Gen<Case> Generator =
			from nodes in Gen.Int[3, 6]
			from healthy in Gen.Bool.Array[nodes]
			from maxRetries in Gen.Int[1, 4]
			from parallelism in Gen.Int[8, 32]
			select new Case(healthy, maxRetries, parallelism);
	}

	private sealed record Trail(Attempt[] Attempts, bool Sniffed, bool Success);

	private static Trail[] RunConcurrently(Case c)
	{
		VirtualCluster cluster = Virtual.Elasticsearch.Bootstrap(c.Nodes);
		for (var i = 0; i < c.Nodes; i++)
		{
			var port = 9200 + i;
			var healthy = c.Healthy[i];
			cluster = cluster.ClientCalls(r => healthy ? r.OnPort(port).SucceedAlways() : r.OnPort(port).FailAlways());
		}

		var virtualized = cluster
			.Sniff(s => s.SucceedAlways())
			.SniffingNodePool()
			.Settings(s => s.DisablePing().ThrowExceptions(false).SniffOnConnectionFault().MaximumRetries(c.MaxRetries));

		var trails = new ConcurrentBag<Trail>();
		_ = Parallel.For(0, c.Parallelism, new ParallelOptions { MaxDegreeOfParallelism = c.Parallelism }, _ =>
		{
			var response = virtualized.ClientCall();
			var audits = response.ApiCallDetails.AuditTrail ?? [];
			var attempts = audits
				.Where(a => a.Event is AuditEvent.HealthyResponse or AuditEvent.BadResponse or AuditEvent.BadRequest)
				.Select(a => new Attempt(a.Event, a.Node!.Uri.Port))
				.ToArray();
			var sniffed = audits.Any(a => a.Event is AuditEvent.SniffOnFail or AuditEvent.SniffOnStaleCluster or AuditEvent.SniffOnStartup);
			trails.Add(new Trail(attempts, sniffed, response.ApiCallDetails.HasSuccessfulStatusCode));
		});

		return [.. trails];
	}

	[Fact]
	public void EveryRequestObeysTheRulesOfASingleRequestWhateverTheOthersDo() =>
		Case.Generator.Sample(c =>
		{
			foreach (var trail in RunConcurrently(c))
			{
				var because = $"{c}: {string.Join(", ", trail.Attempts.Select(a => $"{a.Event}@{a.Port}"))} (sniffed={trail.Sniffed})";

				// The retry budget belongs to the request.
				_ = trail.Attempts.Length.Should().BeLessThanOrEqualTo(Math.Min(c.MaxRetries, c.Nodes - 1) + 1, because);

				// A healthy answer ends the request, so only the last attempt can be healthy.
				_ = trail.Attempts.Take(Math.Max(0, trail.Attempts.Length - 1)).All(a => a.Event != AuditEvent.HealthyResponse).Should().BeTrue(because);
				_ = trail.Success.Should().Be(trail.Attempts.Any(a => a.Event == AuditEvent.HealthyResponse), because);

				// Unless this request reseeded the pool itself, it never goes back to a node it already tried.
				if (!trail.Sniffed)
					_ = trail.Attempts.Select(a => a.Port).Distinct().Count().Should().Be(trail.Attempts.Length, because);
			}
		}, iter: 40);
}
