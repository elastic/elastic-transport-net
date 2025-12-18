// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using static Elastic.Transport.Products.Elasticsearch.ElasticsearchNodeFeatures;

namespace Elastic.Transport.Products.Elasticsearch;

internal sealed class SniffResponse : TransportResponse
{
	// ReSharper disable InconsistentNaming
	public string? cluster_name { get; set; }

	public Dictionary<string, NodeInfo>? nodes { get; set; }

	public IEnumerable<Node> ToNodes(bool forceHttp = false)
	{
		if (nodes == null)
			yield break;

		foreach (var kv in nodes.Where(n => n.Value.HttpEnabled))
		{
			var info = kv.Value;
			var httpEndpoint = info.http?.publish_address;
			if (string.IsNullOrWhiteSpace(httpEndpoint))
				httpEndpoint = kv.Value.http?.bound_address?.FirstOrDefault();
			if (string.IsNullOrWhiteSpace(httpEndpoint))
				continue;

			var uri = SniffParser.ParseToUri(httpEndpoint!, forceHttp);
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
				Name = info.name, Id = kv.Key, Settings = new ReadOnlyDictionary<string, object>(info.settings ?? new Dictionary<string, object>())
			};
			yield return node;
		}
	}
}
