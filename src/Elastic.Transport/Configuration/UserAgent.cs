/*
 * Licensed to Elasticsearch B.V. under one or more contributor
 * license agreements. See the NOTICE file distributed with
 * this work for additional information regarding copyright
 * ownership. Elasticsearch B.V. licenses this file to you under
 * the Apache License, Version 2.0 (the "License"); you may
 * not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *    http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing,
 * software distributed under the License is distributed on an
 * "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
 * KIND, either express or implied.  See the License for the
 * specific language governing permissions and limitations
 * under the License.
 */

using System;
using System.Reflection;
using System.Runtime.InteropServices;
using Elastic.Transport.Extensions;

namespace Elastic.Transport
{
	/// <summary>
	/// Represents the user agent string. Two constructors exists, one to aid with constructing elastic clients standard compliant
	/// user agents and one free form to allow any custom string to be set.
	/// </summary>
	public class UserAgent
	{
		private readonly string _toString;

		private UserAgent(string reposName, Type typeVersionLookup, string[] metadata = null)
		{
			var version = typeVersionLookup.Assembly
				.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
				.InformationalVersion;

			var meta = string.Join("; ", metadata ?? Array.Empty<string>());
			var assemblyName = typeVersionLookup.Assembly.GetName().Name;

			_toString = $"{reposName}/{version} ({RuntimeInformation.OSDescription}; {RuntimeInformation.FrameworkDescription}; {assemblyName}{meta.Trim()})";
		}

		private UserAgent(string fullUserAgentString) => _toString = fullUserAgentString;

		/// <summary> Create a user agent that adhers to the minimum information needed to be elastic standard compliant </summary>
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

		/// <summary> The precalculated string representation of this <see cref="UserAgent"/> instance </summary>
		/// <returns></returns>
		public override string ToString() => _toString;
	}
}
