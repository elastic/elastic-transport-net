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
using Elastic.Transport.VirtualizedCluster.Rules;

namespace Elastic.Transport.Properties.Tests;

/// <summary>One attempted node (a node that fails the connection audits as BadRequest, one that answers with a bad status as BadResponse) in an audit trail: what happened, on which port.</summary>
public readonly record struct Attempt(AuditEvent Event, int Port);

/// <summary>
/// A request against a static pool of <see cref="Nodes"/> nodes on ports 9200.., where <see cref="Healthy"/>[i]
/// says whether node i answers 200 or 503 forever. Pings and sniffing are off, so the only moving parts are
/// node selection and the retry budget.
/// </summary>
public sealed record Scenario(bool[] Healthy, int? MaxRetries)
{
	public int Nodes => Healthy.Length;

	public override string ToString() =>
		$"nodes=[{string.Join("", Healthy.Select(h => h ? 'H' : 'x'))}] maxRetries={MaxRetries?.ToString() ?? "default"}";

	public static readonly Gen<Scenario> Generator =
		from nodes in CsCheck.Gen.Int[2, 8]
		from healthy in CsCheck.Gen.Bool.Array[nodes]
		from maxRetries in CsCheck.Gen.Int[-1, 10]
		select new Scenario(healthy, maxRetries < 0 ? null : maxRetries);
}

/// <summary>What a <see cref="Scenario"/> did: the response status and every node attempt, in order.</summary>
public sealed record RequestOutcome(Scenario Scenario, bool Success, IReadOnlyList<Attempt> Attempts, IReadOnlyList<AuditEvent> Events)
{
	public static RequestOutcome Run(Scenario scenario)
	{
		VirtualCluster cluster = Virtual.Elasticsearch.Bootstrap(scenario.Nodes);
		for (var i = 0; i < scenario.Nodes; i++)
		{
			var port = 9200 + i;
			var healthy = scenario.Healthy[i];
			cluster = cluster.ClientCalls(r => healthy ? r.OnPort(port).SucceedAlways() : r.OnPort(port).FailAlways());
		}

		var response = cluster
			.StaticNodePool()
			.Settings(s =>
			{
				s = s.DisablePing().ThrowExceptions(false);
				return scenario.MaxRetries is { } m ? s.MaximumRetries(m) : s;
			})
			.ClientCall();

		var trail = response.ApiCallDetails.AuditTrail ?? [];
		var attempts = trail
			.Where(a => a.Event is AuditEvent.HealthyResponse or AuditEvent.BadResponse or AuditEvent.BadRequest)
			.Select(a => new Attempt(a.Event, a.Node!.Uri.Port))
			.ToList();

		return new RequestOutcome(scenario, response.ApiCallDetails.HasSuccessfulStatusCode, attempts, trail.Select(a => a.Event).ToList());
	}
}

/// <summary>
/// The reference model: what the state diagram says must happen, written without looking at the pipeline.
/// Nodes are tried in pool order. A request gets min(MaxRetries, nodes - 1) + 1 attempts and stops at the
/// first healthy node.
/// </summary>
public static class Model
{
	public static int AllowedAttempts(Scenario s) => Math.Min(s.MaxRetries ?? int.MaxValue, s.Nodes - 1) + 1;

	public static (bool Success, Attempt[] Attempts) Expected(Scenario s)
	{
		var attempts = new List<Attempt>();
		for (var i = 0; i < AllowedAttempts(s); i++)
		{
			var healthy = s.Healthy[i];
			attempts.Add(new Attempt(healthy ? AuditEvent.HealthyResponse : AuditEvent.BadRequest, 9200 + i));
			if (healthy)
				return (true, attempts.ToArray());
		}
		return (false, attempts.ToArray());
	}
}
