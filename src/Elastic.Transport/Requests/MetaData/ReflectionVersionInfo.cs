// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

using Elastic.Transport.Extensions;

namespace Elastic.Transport;

/// <summary>
///
/// </summary>
internal sealed class ReflectedVersionInfo : VersionInfo
{
	private static readonly SemVersion Empty = new(0, 0, 0);

	/// <summary> The version of Elastic.Transport itself </summary>
	public static readonly string TransportVersion = Create<ReflectedVersionInfo>().ToString();

	private ReflectedVersionInfo(SemVersion version) :
		base(version)
	{
	}

	/// <summary>
	///
	/// </summary>
	/// <typeparam name="T"></typeparam>
	/// <returns></returns>
	public static ReflectedVersionInfo Create<T>()
	{
		var version = DetermineVersionFromType(typeof(T));
		var versionInfo = new ReflectedVersionInfo(version);
		return versionInfo;
	}

	/// <summary>
	///
	/// </summary>
	/// <param name="type"></param>
	/// <returns></returns>
	public static ReflectedVersionInfo Create(Type type)
	{
		var version = DetermineVersionFromType(type);
		var versionInfo = new ReflectedVersionInfo(version);
		return versionInfo;
	}

	private static SemVersion DetermineVersionFromType(Type type)
	{
		try
		{
			// Try to read the full version in 'major.minor.patch[.build][-prerelease][+build]' format. This format is semver2 compliant
			// except for the optional [.build] version number.

			var version = type.Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

			if (!string.IsNullOrEmpty(version))
			{
				// Version string is already in semver format
				// version is guaranteed non-null here due to IsNullOrEmpty check above
				if (SemVersion.TryParse(version!, out var result))
					return result;

				var prefix = GetVersionPrefixPart(version!);

				// Version prefix is not in a valid 'major.minor[.build[.revision]]' form
				if (!Version.TryParse(prefix, out var prefixVersion))
					return Empty;

				// Version prefix '[.revision]' part is not present, but initial semver parsing failed anyway.
				// Nothing we can do here...
				if (prefixVersion.Revision < 0)
					return Empty;

				// Remove non semver compliant '[.revision]'
				version = $"{prefixVersion.Major}.{prefixVersion.Minor}.{prefixVersion.Build}{version!.Substring(prefix.Length)}";
				if (!SemVersion.TryParse(version, out result))
					return Empty;

				// Prepend the 'revision' to metadata
				if (prefixVersion.Revision > 0)
				{
					var meta = $"rev{prefixVersion.Revision}";
					if (result.Metadata.Length != 0)
						meta = $"{meta}.{result.Metadata}";

					result = new SemVersion(result.Major, result.Minor, result.Patch, result.Prerelease, meta);
				}

				return result;
			}
		}
		catch
		{
			// ignore failures and fall through
		}

		try
		{
			// Try to read the assembly version in 'major.minor[.build[.revision]]' format.

			var version = type.Assembly.GetCustomAttribute<AssemblyVersionAttribute>()?.Version;

			if (string.IsNullOrEmpty(version))
				version = type.Assembly.GetName().Version?.ToString();

			if (!string.IsNullOrEmpty(version))
			{
				// version is guaranteed non-null here due to IsNullOrEmpty check above
				var parts = version!.Split('.');

				var major = parts.Length >= 1 && int.TryParse(parts[0], out var majorVal) ? majorVal : 0;
				var minor = parts.Length >= 2 && int.TryParse(parts[1], out var minorVal) ? minorVal : 0;
				var build = parts.Length >= 3 && int.TryParse(parts[2], out var buildVal) ? buildVal : 0;
				var revision = parts.Length >= 4 && int.TryParse(parts[3], out var revisionVal) ? revisionVal : 0;

				// Use 'build' as the semver 'patch' part and add the 'revision' as metadata
				return (revision > 0)
					? new SemVersion(major, minor, build, null, $"rev{revision}")
					: new SemVersion(major, minor, build);
			}
		}
		catch
		{
			// ignore failures and fall through
		}

		return Empty;
	}

	/// <summary>
	///
	/// </summary>
	/// <param name="fullVersionName"></param>
	/// <returns></returns>
	private static string GetVersionPrefixPart(string fullVersionName) =>
		new(fullVersionName.TakeWhile(c => char.IsDigit(c) || c == '.').ToArray());
}

/// <summary>
/// The version of an assembly, read from its metadata: the informational version if there is one, else the assembly version.
/// </summary>
public sealed class ReflectionVersionInfo
{
	private readonly ReflectedVersionInfo _info;

	private ReflectionVersionInfo(ReflectedVersionInfo info) => _info = info;

	/// <summary> The version of Elastic.Transport itself </summary>
	public static readonly string TransportVersion = ReflectedVersionInfo.TransportVersion;

	/// <summary> The version of the assembly that contains <typeparamref name="T"/>. </summary>
	/// <typeparam name="T">A type from the assembly to read the version of.</typeparam>
	public static ReflectionVersionInfo Create<T>() => new(ReflectedVersionInfo.Create<T>());

	/// <summary> The version of the assembly that contains <paramref name="type"/>. </summary>
	/// <param name="type">A type from the assembly to read the version of.</param>
	public static ReflectionVersionInfo Create(Type type) => new(ReflectedVersionInfo.Create(type));

	/// <summary> The major version. </summary>
	public int Major => _info.Major;

	/// <summary> The minor version. </summary>
	public int Minor => _info.Minor;

	/// <summary> The patch version. </summary>
	public int Patch => _info.Patch;

	/// <summary> The pre-release label, if any. </summary>
	public string? Prerelease => _info.Prerelease;

	/// <summary> The build metadata, if any. </summary>
	public string? Metadata => _info.Metadata;

	/// <summary> Whether this is a pre-release version. </summary>
	public bool IsPrerelease => _info.IsPrerelease;

	/// <summary> The version in the form the client meta header carries it: major.minor.patch, with a trailing p for a pre-release. </summary>
	public string ToMetadataHeaderValue() => _info.ToMetadataHeaderValue();

	/// <inheritdoc />
	public override string ToString() => _info.ToString();
}
