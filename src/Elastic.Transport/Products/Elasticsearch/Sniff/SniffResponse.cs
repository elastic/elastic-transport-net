// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Elastic.Transport.Extensions;
using static Elastic.Transport.Products.Elasticsearch.ElasticsearchNodeFeatures;

namespace Elastic.Transport.Products.Elasticsearch.Sniff
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

	internal class SniffResponse : TransportResponseBase
	{
		// ReSharper disable InconsistentNaming
		public string cluster_name { get; set; }

		public Dictionary<string, NodeInfo> nodes { get; set; }

		public IEnumerable<Node> ToNodes(bool forceHttp = false)
		{
			foreach (var kv in nodes.Where(n => n.Value.HttpEnabled))
			{
				var info = kv.Value;
				var httpEndpoint = info.http?.publish_address;
				if (string.IsNullOrWhiteSpace(httpEndpoint))
					httpEndpoint = kv.Value.http?.bound_address.FirstOrDefault();
				if (string.IsNullOrWhiteSpace(httpEndpoint))
					continue;

				var uri = SniffParser.ParseToUri(httpEndpoint, forceHttp);
				var features = new List<string>();
				if (info.MasterEligible)
					features.Add(MasterEligible);
				if (info.HoldsData)
					features.Add(HoldsData);
				if (info.IngestEnabled)
					features.Add(IngestEnabled);
				if (info.HttpEnabled)
					features.Add(HttpEnabled);

				var node = new Node(uri, features)
				{
					Name = info.name, Id = kv.Key, Settings = new ReadOnlyDictionary<string, object>(info.settings)
				};
				yield return node;
			}
		}
	}

	internal class NodeInfo
	{
		public string build_hash { get; set; }
		public string host { get; set; }
		public NodeInfoHttp http { get; set; }
		public string ip { get; set; }
		public string name { get; set; }
		public IList<string> roles { get; set; }
		public IDictionary<string, object> settings { get; set; }
		public string transport_address { get; set; }
		public string version { get; set; }
		internal bool HoldsData => roles?.Contains("data") ?? false;

		internal bool HttpEnabled
		{
			get
			{
				if (settings != null && settings.TryGetValue("http.enabled", out var httpEnabled))
				{
					if (httpEnabled is JsonElement e)
						return e.GetBoolean();
					return Convert.ToBoolean(httpEnabled);
				}

				return http != null;
			}
		}

		internal bool IngestEnabled => roles?.Contains("ingest") ?? false;

		internal bool MasterEligible => roles?.Contains("master") ?? false;
	}

	internal class NodeInfoHttp
	{
		public IList<string> bound_address { get; set; }
		public string publish_address { get; set; }
	}
}
