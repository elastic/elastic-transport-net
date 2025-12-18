// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.Transport.Extensions;

namespace Elastic.Transport;

/// <summary>
///
/// </summary>
public abstract class VersionInfo
{
	private readonly SemVersion _version;

	/// <summary>
	///
	/// </summary>
	public int Major => _version.Major;

	/// <summary>
	///
	/// </summary>
	public int Minor => _version.Minor;

	/// <summary>
	///
	/// </summary>
	public int Patch => _version.Patch;

	/// <summary>
	///
	/// </summary>
	public string? Prerelease => _version.Prerelease;

	/// <summary>
	///
	/// </summary>
	public string? Metadata => _version.Metadata;

	/// <summary>
	///
	/// </summary>
	public bool IsPrerelease => !string.IsNullOrEmpty(_version.Prerelease);

	/// <summary>
	///
	/// </summary>
	/// <param name="version"></param>
	protected VersionInfo(SemVersion version) => _version = version;

	/// <summary> Returns the full version as a semantic version number </summary>
	public override string ToString() => _version.ToString();

	/// <summary> Returns the version in a way that safe to emit as telemetry </summary>
	public string ToMetadataHeaderValue()
	{
		var prefix = $"{Major}.{Minor}.{Patch}";

		return IsPrerelease ? $"{prefix}p" : prefix;
	}
}
