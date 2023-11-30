// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

namespace Elastic.Transport;

/// <summary>
///
/// </summary>
public abstract class VersionInfo
{
	/// <summary>
	///
	/// </summary>
	public int Major { get; }

	/// <summary>
	///
	/// </summary>
	public int Minor { get; }

	/// <summary>
	///
	/// </summary>
	public int Patch { get; }

	/// <summary>
	///
	/// </summary>
	public string? Prerelease { get; }

	/// <summary>
	///
	/// </summary>
	public string? Metadata { get; }

	/// <summary>
	///
	/// </summary>
	public bool IsPrerelease => !string.IsNullOrEmpty(Prerelease);

	/// <summary>
	///
	/// </summary>
	/// <param name="major"></param>
	/// <param name="minor"></param>
	/// <param name="patch"></param>
	/// <param name="prerelease"></param>
	/// <param name="metadata"></param>
	protected VersionInfo(int major, int minor, int patch, string? prerelease, string? metadata)
	{
		Major = major;
		Minor = minor;
		Patch = patch;
		Prerelease = prerelease;
		Metadata = metadata;
	}

	/// <summary>
	///
	/// </summary>
	/// <returns></returns>
	public override string ToString()
	{
		var prefix = $"{Major}.{Minor}.{Patch}";

		return IsPrerelease ? $"{prefix}p" : $"{prefix}";
	}
}
