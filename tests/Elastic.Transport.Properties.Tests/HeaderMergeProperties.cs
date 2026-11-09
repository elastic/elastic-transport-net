// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using CsCheck;
using FluentAssertions;
using Xunit;

namespace Elastic.Transport.Properties.Tests;

/// <summary>Request headers are the transport's headers overlaid with the request's, case-insensitively, plus the opaque id.</summary>
public class HeaderMergeProperties
{
	private static readonly Gen<(string Key, string Value)[]> Headers =
		Gen.Select(Gen.OneOf("X-A", "x-a", "X-B", "X-C", "x-c", "X-D"), Gen.OneOf("1", "2", "3", "four")).Array[0, 5];

	private static NameValueCollection Collection((string Key, string Value)[] pairs)
	{
		var c = new NameValueCollection();
		foreach (var (key, value) in pairs)
			c[key] = value; // set, so a later value for the same key (any casing) replaces an earlier one
		return c;
	}

	private static Dictionary<string, string> Model(NameValueCollection? c) =>
		c is null ? new(StringComparer.OrdinalIgnoreCase) : c.AllKeys.ToDictionary(k => k!, k => c[k]!, StringComparer.OrdinalIgnoreCase);

	private static Dictionary<string, string> Observed(NameValueCollection? c) => Model(c);

	[Fact]
	public void TheResolvedHeadersAreTheTransportsOverlaidWithTheRequestsPlusTheOpaqueId() =>
		Gen.Select(Headers, Headers.Null(), Gen.OneOf<string?>(null, "", "op-1")).Sample((globalPairs, localPairs, opaqueId) =>
		{
			var global = Collection(globalPairs);
			var snapshot = new NameValueCollection(global);
			using var settings = new TransportConfiguration(new Uri("http://localhost:9200")) { Headers = globalPairs.Length == 0 ? null : global };
			var local = new RequestConfiguration { Headers = localPairs is null ? null : Collection(localPairs), OpaqueId = opaqueId };

			var bound = new BoundConfiguration(settings, local, globalPairs.Length == 0 ? null : snapshot);

			var expected = Model(globalPairs.Length == 0 ? null : global);
			if (local.Headers is not null)
				foreach (var (key, value) in Model(local.Headers))
					expected[key] = value;
			if (!string.IsNullOrEmpty(opaqueId))
				expected["X-Opaque-Id"] = opaqueId;

			var because = $"global=[{string.Join(",", globalPairs)}] local=[{string.Join(",", localPairs ?? [])}] opaque={opaqueId}";
			if (expected.Count == 0)
				_ = (bound.Headers is null || bound.Headers.Count == 0).Should().BeTrue(because);
			else
				_ = Observed(bound.Headers).Should().BeEquivalentTo(expected, because);

			// Whatever the request added, the transport's own snapshot is left exactly as it was.
			_ = Observed(snapshot).Should().BeEquivalentTo(Model(Collection(globalPairs)), "the shared snapshot is never written to");
		}, iter: 1000);
}
