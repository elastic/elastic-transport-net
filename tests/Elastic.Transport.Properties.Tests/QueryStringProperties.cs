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

/// <summary>Query strings go through escaping on the way out: whatever the names and values are, they must come back as they were.</summary>
public class QueryStringProperties
{
	private sealed class Parameters : RequestParameters;

	// Includes the characters a query string uses as syntax: & = ? # + % and space.
	private static readonly Gen<string> Name =
		Gen.Int[0x21, 0x7E].Select(i => (char)i).Array[1, 8].Select(c => new string(c));

	private static readonly Gen<string> Value =
		Gen.Int[0x20, 0xD7FF].Select(i => (char)i).Array[1, 20].Select(c => new string(c));

	private static Dictionary<string, string> Parse(string path)
	{
		var query = path[(path.IndexOf('?') + 1)..];
		return query.Split('&').Select(pair => pair.Split('=', 2)).ToDictionary(p => Uri.UnescapeDataString(p[0]), p => Uri.UnescapeDataString(p[1]));
	}

	[Fact]
	public void NamesAndValuesComeBackAsTheyWentWhateverCharactersTheyHold() =>
		// Names are matched case-insensitively (the library holds them in a NameValueCollection), so two names that differ only in
		// case are one parameter. Elasticsearch's are lower case, so this is a limit of the design, not a bug: keep names distinct.
		Gen.Dictionary(Name, Value)[1, 5].Where(d => d.Keys.Select(k => k.ToLowerInvariant()).Distinct().Count() == d.Count).Sample(pairs =>
		{
			var parameters = new Parameters();
			foreach (var (name, value) in pairs)
				parameters.SetQueryString(name, value);

			var path = parameters.CreatePathWithQueryStrings("/index/_search", Settings);

			_ = path.Should().StartWith("/index/_search?");
			_ = path.Count(c => c == '?').Should().Be(1, "escaping must not leave a second ?");
			_ = Parse(path).Should().BeEquivalentTo(pairs);
		}, iter: 1000);

	[Fact]
	public void BooleansAndNumbersAreWrittenTheWayTheServerReadsThem() =>
		Gen.Select(Name, Gen.Bool, Gen.Int).Sample((name, flag, number) =>
		{
			var parameters = new Parameters();
			parameters.SetQueryString(name + "-b", flag);
			parameters.SetQueryString(name + "-n", number);

			var parsed = Parse(parameters.CreatePathWithQueryStrings("/", Settings));

			_ = parsed[name + "-b"].Should().Be(flag ? "true" : "false");
			_ = parsed[name + "-n"].Should().Be(number.ToString(System.Globalization.CultureInfo.InvariantCulture));
		}, iter: 300);

	[Fact]
	public void WithNoParametersThePathIsReturnedUntouched() =>
		Gen.Int[0x21, 0x7E].Select(i => (char)i).Array[0, 20].Select(c => new string(c)).Where(p => !p.Contains('?')).Sample(path =>
			_ = new Parameters().CreatePathWithQueryStrings(path, Settings).Should().Be(path), iter: 300);

	[Fact]
	public void ThePathItselfMayNeverCarryAQueryString() =>
		Gen.Select(Name.Where(n => !n.Contains('?')), Name.Where(n => !n.Contains('?'))).Sample((before, after) =>
			_ = ((Action)(() => new Parameters().CreatePathWithQueryStrings(before + "?" + after, Settings))).Should().Throw<ArgumentException>(), iter: 100);

	private static readonly TransportConfiguration Settings = new(new Uri("http://localhost:9200"));
}
