// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Linq;
using System.Threading.Tasks;
using CsCheck;
using FluentAssertions;
using Nullean.Entities.CsCheck;
using Xunit;

namespace Elastic.Transport.Properties.Tests;

public class StaticPoolRetryTests
{
	// BUG 1: RequestPipeline.NextNode receives attemptedNodes by value into an iterator, so the retry budget is never
	// consulted while walking the nodes. Shrunk witness: nodes=[xH] maxRetries=0 -> the model expects 1 attempt and
	// a failure, the pipeline goes on to node 2 and succeeds. Un-skipped by the Stage 3 fix.
	private const string Bug1 = "BUG 1: MaxRetries is not enforced while iterating nodes";

	/// <summary>The pipeline and the diagram agree, for every generated scenario.</summary>
	[Fact(Skip = Bug1)]
	public void PipelineMatchesTheModel() =>
		Scenario.Generator.Sample(scenario =>
		{
			var outcome = RequestOutcome.Run(scenario);
			var (success, attempts) = Model.Expected(scenario);

			_ = outcome.Success.Should().Be(success, scenario.ToString());
			_ = outcome.Attempts.Should().Equal(attempts, scenario.ToString());
		}, iter: 500);

	/// <summary>Same model check where the retry budget cannot bind (default MaxRetries): holds today.</summary>
	[Fact]
	public void PipelineMatchesTheModelWithDefaultRetries() =>
		Scenario.Generator.Select(s => s with { MaxRetries = null }).Sample(scenario =>
		{
			var outcome = RequestOutcome.Run(scenario);
			var (success, attempts) = Model.Expected(scenario);

			_ = outcome.Success.Should().Be(success, scenario.ToString());
			_ = outcome.Attempts.Should().Equal(attempts, scenario.ToString());
		}, iter: 300);

	/// <summary>The same scenarios, judged by named rules instead of an exact expected trail.</summary>
	[Fact]
	public async Task EveryFinishedRequestKeepsItsRules() =>
		await AutoTest
			.For(RequestOutcomeConstraints.Instance, Scenario.Generator.Select(RequestOutcome.Run))
			.RunAsync();

	[Fact(Skip = Bug1)]
	public async Task NoRequestExceedsItsRetryBudget() =>
		await AutoTest
			.For(RetryBudgetConstraints.Instance, Scenario.Generator.Select(RequestOutcome.Run))
			.RunAsync();
}
