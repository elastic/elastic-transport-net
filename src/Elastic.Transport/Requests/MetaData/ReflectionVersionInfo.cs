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
		try
		{
			var productVersion = FileVersionInfo.GetVersionInfo(type.GetTypeInfo().Assembly.Location)?.ProductVersion ?? EmptyVersion;

			if (productVersion == EmptyVersion)
				productVersion = Assembly.GetAssembly(type).GetName().Version.ToString();

			var match = VersionRegex.Match(productVersion);

			return match.Success ? match.Value : EmptyVersion;
		}
		catch
		{
			// ignore failures and fall through
		}

		return EmptyVersion;
	}
}
