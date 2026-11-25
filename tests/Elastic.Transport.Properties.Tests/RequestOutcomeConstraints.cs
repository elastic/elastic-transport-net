// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Linq;
using Elastic.Transport.Diagnostics.Auditing;
using Nullean.Entities;

namespace Elastic.Transport.Properties.Tests;

/// <summary>Rules every finished request must satisfy, whatever the cluster looked like.</summary>
[Constraints<RequestOutcome>]
public static partial class RequestOutcomeConstraints
{
	public static readonly Constraint<RequestOutcome> StopsAtTheFirstHealthyNode = Constraints<RequestOutcome>.Always(
		o => o.Attempts.Count == 0 || o.Attempts.Take(o.Attempts.Count - 1).All(a => a.Event == AuditEvent.BadRequest),
		"Only the last attempt may be healthy: a healthy answer ends the request.");

	public static readonly Constraint<RequestOutcome> NoNodeIsTriedTwice = Constraints<RequestOutcome>.Always(
		o => o.Attempts.Select(a => a.Port).Distinct().Count() == o.Attempts.Count,
		"Without a reseed, a request never goes back to a node it already tried.");

	public static readonly Constraint<RequestOutcome> SuccessMeansAHealthyAttempt = Constraints<RequestOutcome>.Always(
		o => o.Success == o.Attempts.Any(a => a.Event == AuditEvent.HealthyResponse),
		"The response succeeds exactly when some attempt was healthy.");
}

/// <summary>The retry budget on its own, because it is the rule that once failed (see StaticPoolRetryTests).</summary>
[Constraints<RequestOutcome>]
public static partial class RetryBudgetConstraints
{
	public static readonly Constraint<RequestOutcome> NeverExceedsTheRetryBudget = Constraints<RequestOutcome>.Always(
		o => o.Attempts.Count <= Model.AllowedAttempts(o.Scenario),
		"A request makes at most min(MaxRetries, nodes - 1) + 1 attempts.");
}
