// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json.Serialization;
using static Elastic.Transport.Products.Elasticsearch.ElasticsearchNodeFeatures;

namespace Elastic.Transport.Products.Elasticsearch;

internal sealed class SniffResponse : TransportResponse
{
	[JsonPropertyName("cluster_name")]
	public string? ClusterName { get; set; }

	[JsonPropertyName("nodes")]
	public Dictionary<string, NodeInfo>? Nodes { get; set; }

	public IEnumerable<Node> ToNodes(bool forceHttp = false)
	{
		if (Nodes == null)
			yield break;

		foreach (var kv in Nodes.Where(n => n.Value.HttpEnabled))
		{
			var info = kv.Value;
			var httpEndpoint = info.Http?.PublishAddress;
			if (string.IsNullOrWhiteSpace(httpEndpoint))
				httpEndpoint = kv.Value.Http?.BoundAddress?.FirstOrDefault();
			if (httpEndpoint is null || string.IsNullOrWhiteSpace(httpEndpoint))
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
				Name = info.Name, Id = kv.Key, Settings = new ReadOnlyDictionary<string, object>(info.Settings ?? new Dictionary<string, object>())
			};
			yield return node;
		}
	}
}
