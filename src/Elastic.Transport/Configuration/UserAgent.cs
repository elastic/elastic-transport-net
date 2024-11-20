// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Reflection;

#if NETFRAMEWORK

using Elastic.Transport.Extensions;

#else

using System.Runtime.InteropServices;

#endif

namespace Elastic.Transport;

/// <summary>
/// Represents the user agent string. Two constructors exists, one to aid with constructing elastic clients standard compliant
/// user agents and one free form to allow any custom string to be set.
/// </summary>
public sealed class UserAgent
{
	private readonly string _toString;

	private UserAgent(string reposName, Type typeVersionLookup, string[]? metadata = null)
	{
		var version = typeVersionLookup.Assembly
			.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
			.InformationalVersion;

		var meta = string.Join("; ", metadata ?? []);
		var assemblyName = typeVersionLookup.Assembly.GetName().Name;

		_toString = $"{reposName}/{version} ({RuntimeInformation.OSDescription}; {RuntimeInformation.FrameworkDescription}; {assemblyName}{meta.Trim()})";
	}

	private UserAgent(string fullUserAgentString) => _toString = fullUserAgentString;

	/// <summary> Create a user agent that adheres to the minimum information needed to be elastic standard compliant </summary>
	/// <param name="reposName">The repos name uniquely identifies the origin of the client</param>
	/// <param name="typeVersionLookup">
	/// Use <see cref="Type"/>'s assembly <see cref="AssemblyInformationalVersionAttribute"/>
	/// to inject version information into the header
	/// </param>
	public static UserAgent Create(string reposName, Type typeVersionLookup) => new UserAgent(reposName, typeVersionLookup);

	/// <summary> <inheritdoc cref="Create(string,System.Type)"/> </summary>
	public static UserAgent Create(string reposName, Type typeVersionLookup, string[] metadata) => new UserAgent(reposName, typeVersionLookup, metadata);

	/// <summary> Create a user string that does not confirm to elastic client standards </summary>
	public static UserAgent Create(string fullUserAgentString) => new UserAgent(fullUserAgentString);

	/// <summary> The pre=calculated string representation of this <see cref="UserAgent"/> instance </summary>
	/// <returns></returns>
	public override string ToString() => _toString;
}
