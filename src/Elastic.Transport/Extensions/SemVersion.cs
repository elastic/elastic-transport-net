// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Elastic.Transport.Extensions;

internal sealed class SemVersion
{
	// https://semver.org/#is-there-a-suggested-regular-expression-regex-to-check-a-semver-string
	private static readonly Regex Regex = new(@"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-((?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*)(?:\.(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*))*))?(?:\+([0-9a-zA-Z-]+(?:\.[0-9a-zA-Z-]+)*))?$");

	public int Major { get; }

	public int Minor { get; }

	public int Patch { get; }

	public string Prerelease { get; }

	public string Metadata { get; }

	public SemVersion(int major, int minor, int patch)
	{
		Major = major;
		Minor = minor;
		Patch = patch;
		Prerelease = string.Empty;
		Metadata = string.Empty;
	}

	public SemVersion(int major, int minor, int patch, string? prerelease)
	{
		Major = major;
		Minor = minor;
		Patch = patch;
		Prerelease = prerelease ?? string.Empty;
		Metadata = string.Empty;
	}

	public SemVersion(int major, int minor, int patch, string? prerelease, string? metadata)
	{
		Major = major;
		Minor = minor;
		Patch = patch;
		Prerelease = prerelease ?? string.Empty;
		Metadata = metadata ?? string.Empty;
	}

	public static bool TryParse(string input, [NotNullWhen(true)] out SemVersion? version)
	{
		version = null;

		var match = Regex.Match(input);
		if (!match.Success)
			return false;

		if (!int.TryParse(match.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var major))
			return false;
		if (!int.TryParse(match.Groups[2].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var minor))
			return false;
		if (!int.TryParse(match.Groups[3].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var patch))
			return false;

		version = new SemVersion(major, minor, patch, match.Groups[4].Value, match.Groups[5].Value);

		return true;
	}
}
