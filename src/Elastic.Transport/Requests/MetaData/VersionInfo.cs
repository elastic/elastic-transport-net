// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Linq;

namespace Elastic.Transport
{
	/// <summary>
	/// 
	/// </summary>
	public abstract class VersionInfo
	{
		/// <summary>
		/// 
		/// </summary>
		protected const string EmptyVersion = "0.0.0";

		/// <summary>
		/// 
		/// </summary>
		public Version Version { get; protected set; }

		/// <summary>
		/// 
		/// </summary>
		public bool IsPrerelease { get; protected set; }

		/// <summary>
		/// 
		/// </summary>
		/// <param name="fullVersion"></param>
		/// <exception cref="ArgumentException"></exception>
		protected void StoreVersion(string fullVersion)
		{
			if (string.IsNullOrEmpty(fullVersion))
				fullVersion = EmptyVersion;

			var clientVersion = GetParsableVersionPart(fullVersion);

			if (!Version.TryParse(clientVersion, out var parsedVersion))
				throw new ArgumentException("Invalid version string", nameof(fullVersion));

			var finalVersion = parsedVersion;

			if (parsedVersion.Minor == -1 || parsedVersion.Build == -1)
				finalVersion = new Version(parsedVersion.Major, parsedVersion.Minor > -1
					? parsedVersion.Minor
					: 0, parsedVersion.Build > -1
						? parsedVersion.Build
						: 0);

			Version = finalVersion;
			IsPrerelease = ContainsPrerelease(fullVersion);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="version"></param>
		/// <returns></returns>
		protected virtual bool ContainsPrerelease(string version) => version.Contains("-");

		/// <summary>
		/// 
		/// </summary>
		/// <param name="fullVersionName"></param>
		/// <returns></returns>
		private static string GetParsableVersionPart(string fullVersionName) =>
			new(fullVersionName.TakeWhile(c => char.IsDigit(c) || c == '.').ToArray());

		/// <summary>
		/// 
		/// </summary>
		/// <returns></returns>
		public override string ToString() => IsPrerelease ? Version.ToString() + "p" : Version.ToString();
	}
}
