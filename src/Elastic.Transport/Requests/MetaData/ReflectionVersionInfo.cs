// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Elastic.Transport
{
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
				var productVersion = "8.0.0-alpha.8+02b315d290415a4eb153beb827a879d037e904f6 (Microsoft Windows 10.0.19044; .NET 6.0.4; Elastic.Clients.Elasticsearch)"; //FileVersionInfo.GetVersionInfo(type.GetTypeInfo().Assembly.Location)?.ProductVersion ?? EmptyVersion;

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
}
