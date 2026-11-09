// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Linq;
using System.Text.RegularExpressions;
using CsCheck;
using Elastic.Transport.Extensions;
using FluentAssertions;
using Xunit;

namespace Elastic.Transport.Properties.Tests;

/// <summary>
/// SemVersion is public API and the parser behind the client meta header. Its contract is semver 2.0: here it is as the
/// official regular expression and the precedence rules of section 11, written without looking at the implementation.
/// </summary>
public partial class SemVersionProperties
{
	// https://semver.org, the regular expression offered for checking a version string.
	[GeneratedRegex(@"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-((?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*)(?:\.(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*))*))?(?:\+([0-9a-zA-Z-]+(?:\.[0-9a-zA-Z-]+)*))?$")]
	private static partial Regex Official();

	private const string AlnumDash = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ-";

	private static readonly Gen<string> Numeric = Gen.OneOf(Gen.Const("0"), Gen.Int[1, 99999].Select(i => i.ToString()));

	private static readonly Gen<string> Alphanumeric =
		Gen.Int[0, AlnumDash.Length - 1].Select(i => AlnumDash[i]).Array[1, 6].Select(c => new string(c))
			.Where(s => s.Any(ch => !char.IsDigit(ch))); // at least one letter or hyphen, or it would be numeric

	private static readonly Gen<string> Identifier = Gen.OneOf(Numeric, Alphanumeric);

	private static readonly Gen<string?> Prerelease =
		Gen.Frequency((3, Gen.Const(default(string))), (2, Identifier.Array[1, 4].Select(ids => (string?)string.Join(".", ids))));

	private static readonly Gen<string?> Metadata =
		Gen.Frequency((3, Gen.Const(default(string))), (1, Gen.Int[0, AlnumDash.Length - 1].Select(i => AlnumDash[i]).Array[1, 5].Select(c => (string?)new string(c)).Array[1, 3].Select(p => (string?)string.Join(".", p))));

	private static readonly Gen<SemVersion> Version =
		from major in Gen.Int[0, 20]
		from minor in Gen.Int[0, 20]
		from patch in Gen.Int[0, 20]
		from pre in Prerelease
		from meta in Metadata
		select new SemVersion(major, minor, patch, pre, meta);

	/// <summary>Semver 2.0 section 11, as a function: negative, zero or positive.</summary>
	private static int Precedence(SemVersion a, SemVersion b)
	{
		var core = a.Major.CompareTo(b.Major);
		if (core != 0) return core;
		core = a.Minor.CompareTo(b.Minor);
		if (core != 0) return core;
		core = a.Patch.CompareTo(b.Patch);
		if (core != 0) return core;

		var pa = string.IsNullOrEmpty(a.Prerelease) ? null : a.Prerelease.Split('.');
		var pb = string.IsNullOrEmpty(b.Prerelease) ? null : b.Prerelease.Split('.');
		if (pa is null && pb is null) return 0;
		if (pa is null) return 1;   // a release outranks any of its pre-releases
		if (pb is null) return -1;

		for (var i = 0; i < Math.Min(pa.Length, pb.Length); i++)
		{
			var an = int.TryParse(pa[i], out var x);
			var bn = int.TryParse(pb[i], out var y);
			var c = an && bn ? x.CompareTo(y)
				: an ? -1      // numeric identifiers rank below alphanumeric ones
				: bn ? 1
				: string.CompareOrdinal(pa[i], pb[i]);
			if (c != 0) return c;
		}
		return pa.Length.CompareTo(pb.Length);
	}

	[Fact]
	public void AVersionPrintedAndParsedIsTheSameVersion() =>
		Version.Sample(v =>
		{
			_ = SemVersion.TryParse(v.ToString(), out var parsed).Should().BeTrue(v.ToString());
			_ = (parsed!.Major, parsed.Minor, parsed.Patch, parsed.Prerelease ?? "", parsed.Metadata ?? "")
				.Should().Be((v.Major, v.Minor, v.Patch, v.Prerelease ?? "", v.Metadata ?? ""), v.ToString());
		}, iter: 1000);

	[Fact]
	public void EveryGeneratedVersionMatchesTheOfficialRegex() =>
		Version.Sample(v => _ = Official().IsMatch(v.ToString()).Should().BeTrue(v.ToString()), iter: 500);

	[Fact]
	public void TheParserAcceptsExactlyWhatTheOfficialRegexAccepts() =>
		// Strings built from the characters that matter, so most are near misses: leading zeros, empty parts, stray dots.
		Gen.Char["0123456789.-+abZ"].Array[0, 14].Select(c => new string(c)).Sample(text =>
		{
			var parsed = SemVersion.TryParse(text, out _);

			// The regex allows any number of digits; SemVersion holds int components, so a core number beyond int.MaxValue is
			// the one thing it refuses that the regex accepts. A limit of the type, stated here so it cannot hide a real difference.
			var match = Official().Match(text);
			var fitsInInts = match.Success && Enumerable.Range(1, 3).All(g => long.Parse(match.Groups[g].Value) <= int.MaxValue);

			_ = parsed.Should().Be(fitsInInts, $"'{text}'");
		}, iter: 5000);

	[Fact]
	public void PrecedenceFollowsTheSemverRules() =>
		Gen.Select(Version, Version).Sample((a, b) =>
			_ = Math.Sign(a.CompareByPrecedence(b)).Should().Be(Math.Sign(Precedence(a, b)), $"{a} vs {b}"), iter: 2000);

	[Fact]
	public void PrecedenceIsAProperOrdering() =>
		Gen.Select(Version, Version, Version).Sample((a, b, c) =>
		{
			_ = Math.Sign(a.CompareByPrecedence(a)).Should().Be(0, "reflexive");
			_ = Math.Sign(a.CompareByPrecedence(b)).Should().Be(-Math.Sign(b.CompareByPrecedence(a)), "antisymmetric");
			if (a.CompareByPrecedence(b) <= 0 && b.CompareByPrecedence(c) <= 0)
				_ = a.CompareByPrecedence(c).Should().BeLessThanOrEqualTo(0, "transitive");
		}, iter: 2000);

	[Fact]
	public void BuildMetadataNeverChangesPrecedence() =>
		Gen.Select(Version, Metadata).Sample((v, meta) =>
			_ = v.CompareByPrecedence(new SemVersion(v.Major, v.Minor, v.Patch, v.Prerelease, meta)).Should().Be(0, $"{v} with metadata {meta}"), iter: 500);

	[Fact]
	public void APreReleaseAlwaysPrecedesItsRelease() =>
		Gen.Select(Version, Identifier).Sample((v, id) =>
			_ = new SemVersion(v.Major, v.Minor, v.Patch, id).CompareByPrecedence(new SemVersion(v.Major, v.Minor, v.Patch)).Should().BeNegative(), iter: 500);

	[Fact]
	public void CompareToIsZeroExactlyWhenTheVersionsAreEqual() =>
		Gen.Select(Version, Version).Sample((a, b) =>
			_ = (a.CompareTo(b) == 0).Should().Be(a.Equals(b), $"{a} vs {b}"), iter: 2000);
}
