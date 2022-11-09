// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Text.RegularExpressions;
using Elastic.Transport.Extensions;

namespace Elastic.Transport.Products.Elasticsearch
{
	/// <summary>
	/// Elasticsearch returns addresses in the form of
	/// <para>[fqdn]/ip:port number</para>
	/// This helper parses it to <see cref="Uri"/>
	/// </summary>
	public static class SniffParser
	{
		/// <summary> A regular expression that captures <c>fqdn</c>, <c>ip</c> and <c>por</c> </summary>
		public static Regex AddressRegex { get; } =
			new Regex(@"^((?<fqdn>[^/]+)/)?(?<ip>[^:]+|\[[\da-fA-F:\.]+\]):(?<port>\d+)$");

		/// <summary>
		/// Elasticsearch returns addresses in the form of
		/// <para>[fqdn]/ip:port number</para>
		/// This helper parses it to <see cref="Uri"/>
		/// </summary>
		public static Uri ParseToUri(string boundAddress, bool forceHttp)
		{
			if (boundAddress == null) throw new ArgumentNullException(nameof(boundAddress));

			var suffix = forceHttp ? "s" : string.Empty;
			var match = AddressRegex.Match(boundAddress);
			if (!match.Success) throw new Exception($"Can not parse bound_address: {boundAddress} to Uri");

			var fqdn = match.Groups["fqdn"].Value.Trim();
			var ip = match.Groups["ip"].Value.Trim();
			var port = match.Groups["port"].Value.Trim();
			var host = !fqdn.IsNullOrEmpty() ? fqdn : ip;

			return new Uri($"http{suffix}://{host}:{port}");
		}
	}
}
