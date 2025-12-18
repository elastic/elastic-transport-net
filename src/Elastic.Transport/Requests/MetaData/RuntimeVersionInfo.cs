// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

// Adapted from BenchmarkDotNet source https://github.com/dotnet/BenchmarkDotNet/blob/master/src/BenchmarkDotNet/Environments/Runtimes/CoreRuntime.cs

#region BenchmarkDotNet License https://github.com/dotnet/BenchmarkDotNet/blob/master/LICENSE.md

// The MIT License
// Copyright (c) 2013–2020.NET Foundation and contributors

// Permission is hereby granted, free of charge, to any person obtaining a copy of this software
// and associated documentation files (the "Software"), to deal in the Software without restriction,
// including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense,
// and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so,
// subject to the following conditions:

// The above copyright notice and this permission notice shall be included in all copies or substantial
// portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT
// LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
// IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
// WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE
// SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

#endregion BenchmarkDotNet License https://github.com/dotnet/BenchmarkDotNet/blob/master/LICENSE.md

using System;
using System.Diagnostics.CodeAnalysis;
using Elastic.Transport.Extensions;

#if !NETFRAMEWORK

using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

#else

using Microsoft.Win32;
using System.Linq;

#endif

namespace Elastic.Transport;

/// <summary>
/// Represents the current .NET Runtime version.
/// </summary>
internal sealed class RuntimeVersionInfo : VersionInfo
{
	private static readonly SemVersion Empty = new(0, 0, 0);

	public RuntimeVersionInfo() : this(GetRuntimeVersion())
	{
	}

	private RuntimeVersionInfo(SemVersion version) :
		// We don't care about metadata
		base(version.Update(null, null, null, null, string.Empty))
	{
	}

	private static SemVersion GetRuntimeVersion()
	{
#if NETFRAMEWORK
		var version = GetFullFrameworkRuntime();
#else
		var version = GetNetCoreVersion();

#endif

		if (version is null || !SemVersion.TryParse(version, out var result))
			return Empty;

		// 5.0.1 FrameworkDescription returns .NET 5.0.1-servicing.20575.16, so we special case servicing as
		// NOT prerelease by converting the prerelease part to metadata
		if (result.Prerelease.Contains("-servicing"))
			return new SemVersion(result.Major, result.Minor, result.Patch, null, result.Prerelease);

		return result;
	}

#if !NETFRAMEWORK

	private static string? GetNetCoreVersion()
	{
		// for .NET 5+ we can use Environment.Version
		if (Environment.Version.Major >= 5)
		{
			const string dotNet = ".NET ";
			if (RuntimeInformation.FrameworkDescription.Contains(dotNet, StringComparison.OrdinalIgnoreCase))
				return RuntimeInformation.FrameworkDescription.Substring(dotNet.Length);
		}
		// next, try using file version info
		//At this point, we can't identify whether this is a prerelease, but a version is better than nothing!

		var frameworkName = Assembly.GetEntryAssembly()?.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName;
		if (frameworkName is not null && TryGetVersionFromFrameworkName(frameworkName, out var runtimeVersion))
			return runtimeVersion;

		if (IsRunningInContainer)
		{
			var dotNetVersion = Environment.GetEnvironmentVariable("DOTNET_VERSION");
			var aspNetCoreVersion = Environment.GetEnvironmentVariable("ASPNETCORE_VERSION");

			return dotNetVersion ?? aspNetCoreVersion;
		}

		return null;
	}

	// sample input:
	// .NETCoreApp,Version=v2.0
	// .NETCoreApp,Version=v2.1
	private static bool TryGetVersionFromFrameworkName(string frameworkName, out string? runtimeVersion)
	{
		const string versionPrefix = ".NETCoreApp,Version=v";
		if (!string.IsNullOrEmpty(frameworkName) && frameworkName.StartsWith(versionPrefix, StringComparison.Ordinal))
		{
			runtimeVersion = frameworkName.Substring(versionPrefix.Length);
			return true;
		}

		runtimeVersion = null;
		return false;
	}

	private static bool IsRunningInContainer => string.Equals(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"), "true", StringComparison.Ordinal);
#endif

#if NETFRAMEWORK
	private static string GetFullFrameworkRuntime()
	{
		const string subkey = @"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full\";

		using (var ndpKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32).OpenSubKey(subkey))
		{
			if (ndpKey != null && ndpKey.GetValue("Release") != null)
			{
				var version = CheckFor45PlusVersion((int)ndpKey.GetValue("Release"));

				if (!string.IsNullOrEmpty(version))
					// version is guaranteed non-null here due to IsNullOrEmpty check above
					return version!;
			}
		}

		var fullName = RuntimeInformation.FrameworkDescription;
		var servicingVersion = new string(fullName.SkipWhile(c => !char.IsDigit(c)).ToArray());
		var servicingVersionRelease = MapToReleaseVersion(servicingVersion);

		return servicingVersionRelease;

		static string MapToReleaseVersion(string servicingVersion)
		{
			// the following code assumes that .NET 4.6.1 is the oldest supported version
			if (string.Compare(servicingVersion, "4.6.2") < 0)
				return "4.6.1";
			if (string.Compare(servicingVersion, "4.7") < 0)
				return "4.6.2";
			if (string.Compare(servicingVersion, "4.7.1") < 0)
				return "4.7";
			if (string.Compare(servicingVersion, "4.7.2") < 0)
				return "4.7.1";
			if (string.Compare(servicingVersion, "4.8") < 0)
				return "4.7.2";

			return "4.8.0"; // most probably the last major release of Full .NET Framework
		}

		// Checking the version using >= enables forward compatibility.
		static string? CheckFor45PlusVersion(int releaseKey)
		{
			if (releaseKey >= 528040)
				return "4.8.0";
			if (releaseKey >= 461808)
				return "4.7.2";
			if (releaseKey >= 461308)
				return "4.7.1";
			if (releaseKey >= 460798)
				return "4.7";
			if (releaseKey >= 394802)
				return "4.6.2";
			if (releaseKey >= 394254)
				return "4.6.1";
			if (releaseKey >= 393295)
				return "4.6";
			if (releaseKey >= 379893)
				return "4.5.2";
			if (releaseKey >= 378675)
				return "4.5.1";
			if (releaseKey >= 378389)
				return "4.5.0";
			// This code should never execute. A non-null release key should mean
			// that 4.5 or later is installed.
			return null;
		}
	}
#endif
}
