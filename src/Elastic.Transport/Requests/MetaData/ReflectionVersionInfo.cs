// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Elastic.Transport;

internal sealed class ReflectionVersionInfo : VersionInfo
{
	private static readonly Regex VersionRegex = new(@"^\d+\.\d+\.\d\-?");

	public static readonly ReflectionVersionInfo Empty = new() { Version = new Version(0, 0, 0), IsPrerelease = false };

	private ReflectionVersionInfo() { }

	public static ReflectionVersionInfo Create<T>()
	{
		var fullVersion = DetermineVersionFromType(typeof(T));
		var clientVersion = new ReflectionVersionInfo();
		clientVersion.StoreVersion(fullVersion);
		return clientVersion;
	}

	public static ReflectionVersionInfo Create(Type type)
	{
		var fullVersion = DetermineVersionFromType(type);
		var clientVersion = new ReflectionVersionInfo();
		clientVersion.StoreVersion(fullVersion);
		return clientVersion;
	}

	private static string DetermineVersionFromType(Type type)
	{
		var productVersion = EmptyVersion;

		try
		{
			productVersion = type.Assembly?.GetCustomAttribute<AssemblyVersionAttribute>()?.Version ?? EmptyVersion;
		}
		catch
		{
			// ignore failures and fall through
		}

		try
		{
			if (productVersion == EmptyVersion)
				productVersion = FileVersionInfo.GetVersionInfo(type.Assembly.Location)?.ProductVersion ?? EmptyVersion;
		}
		catch
		{
			// ignore failures and fall through
		}

		try
		{
			// This fallback may not include the minor version numbers
			if (productVersion == EmptyVersion)
				productVersion = type.Assembly.GetName()?.Version?.ToString() ?? EmptyVersion;
		}
		catch
		{
			// ignore failures and fall through
		}

		if (productVersion == EmptyVersion) return EmptyVersion;

		var match = VersionRegex.Match(productVersion);

		return match.Success ? match.Value : EmptyVersion;
	}
}
