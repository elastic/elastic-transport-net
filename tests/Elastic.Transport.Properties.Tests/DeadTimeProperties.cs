// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using CsCheck;
using FluentAssertions;
using Xunit;

namespace Elastic.Transport.Properties.Tests;

/// <summary>How long a failed node stays out of rotation: it backs off exponentially, up to a ceiling.</summary>
public class DeadTimeProperties
{
	private static readonly Gen<(int Attempts, int? MinSeconds, int? MaxSeconds)> Inputs =
		Gen.Select(Gen.Int[1, 60], Gen.Int[1, 300].Nullable(), Gen.Int[1, 7200].Nullable());

	private static double ExpectedMilliseconds(int attempts, int? min, int? max)
	{
		var timeout = (min is { } m ? TimeSpan.FromSeconds(m) : TimeSpan.FromSeconds(60)).TotalMilliseconds;
		var ceiling = (max is { } x ? TimeSpan.FromSeconds(x) : TimeSpan.FromMinutes(30)).TotalMilliseconds;
		// Every second failure doubles the wait.
		return Math.Min(timeout * Math.Pow(2, attempts / 2.0), ceiling);
	}

	private static (DateTimeOffset Before, DateTimeOffset Result, DateTimeOffset After) Ask(int attempts, int? min, int? max)
	{
		var before = DateTimeOffset.UtcNow;
		var result = DefaultDateTimeProvider.Default.DeadTime(attempts, min is { } m ? TimeSpan.FromSeconds(m) : null, max is { } x ? TimeSpan.FromSeconds(x) : null);
		return (before, result, DateTimeOffset.UtcNow);
	}

	[Fact]
	public void TheWaitIsTheDocumentedBackOffCappedAtTheMaximum() =>
		Inputs.Sample(i =>
		{
			var (before, result, after) = Ask(i.Attempts, i.MinSeconds, i.MaxSeconds);
			var expected = TimeSpan.FromMilliseconds(ExpectedMilliseconds(i.Attempts, i.MinSeconds, i.MaxSeconds));

			_ = result.Should().BeOnOrAfter(before + expected - TimeSpan.FromMilliseconds(1));
			_ = result.Should().BeOnOrBefore(after + expected + TimeSpan.FromMilliseconds(1));
		}, iter: 2000);

	[Fact]
	public void MoreFailuresNeverMeanAShorterWait() =>
		Gen.Select(Gen.Int[1, 59], Gen.Int[1, 300].Nullable(), Gen.Int[1, 7200].Nullable()).Sample(i =>
		{
			var shorter = ExpectedMilliseconds(i.Item1, i.Item2, i.Item3);
			var longer = ExpectedMilliseconds(i.Item1 + 1, i.Item2, i.Item3);

			_ = longer.Should().BeGreaterThanOrEqualTo(shorter);

			var (b1, r1, _) = Ask(i.Item1, i.Item2, i.Item3);
			var (_, r2, a2) = Ask(i.Item1 + 1, i.Item2, i.Item3);
			_ = (r2 - a2).Should().BeGreaterThanOrEqualTo(r1 - b1 - TimeSpan.FromMilliseconds(5), "measured against the clock at each call");
		}, iter: 500);

	[Fact]
	public void NoWaitEverExceedsTheMaximum() =>
		Inputs.Where(i => i.MaxSeconds is not null).Sample(i =>
		{
			var (_, result, after) = Ask(i.Attempts, i.MinSeconds, i.MaxSeconds);

			_ = (result - after).Should().BeLessThanOrEqualTo(TimeSpan.FromSeconds(i.MaxSeconds!.Value) + TimeSpan.FromMilliseconds(1));
		}, iter: 1000);
}
