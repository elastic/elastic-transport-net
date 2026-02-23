// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
// https://raw.githubusercontent.com/dotnet/core-setup/master/src/managed/Microsoft.DotNet.PlatformAbstractions/Native/NativeMethods.Windows.cs

#if NETFRAMEWORK
using System;
using System.Linq;
using System.Reflection;

namespace Elastic.Transport.Extensions;

internal static class RuntimeInformation
{
	public static string FrameworkDescription
	{
		get
		{
			if (field == null)
			{
				var assemblyFileVersionAttribute =
					((AssemblyFileVersionAttribute[])Attribute.GetCustomAttributes(
						typeof(object).Assembly,
						typeof(AssemblyFileVersionAttribute)))
					.OrderByDescending(a => a.Version)
					.First();
				field = $".NET Framework {assemblyFileVersionAttribute.Version}";
			}
			return field;
		}
	}

	public static string OSDescription
	{
		get
		{
			if (field == null)
			{
				var platform = (int)Environment.OSVersion.Platform;
				var isWindows = platform is not 4 and not 6 and not 128;
				field = isWindows ? NativeMethods.Windows.RtlGetVersion() ?? "Microsoft Windows" : Environment.OSVersion.VersionString;
			}
			return field;
		}
	}
}
#endif
