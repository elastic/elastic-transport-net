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
using System.Collections.Generic;
using System.Linq;
using Elastic.Transport.Products.Elasticsearch;

namespace Elastic.Transport.VirtualizedCluster.Products.Elasticsearch
{
	/// <summary> A static util method to create an Elasticsearch sniff response. </summary>
	public static class ElasticsearchSniffResponseFactory
	{
		private static string ClusterName => "elasticsearch-test-cluster";

		/// <inheritdoc cref="IMockProductRegistration.CreateSniffResponseBytes"/>>
		public static byte[] Create(IEnumerable<Node> nodes, string elasticsearchVersion,string publishAddressOverride, bool randomFqdn = false)
		{
			var response = new
			{
				cluster_name = ClusterName,
				nodes = SniffResponseNodes(nodes, elasticsearchVersion, publishAddressOverride, randomFqdn)
			};
			using (var ms = TransportConfiguration.DefaultMemoryStreamFactory.Create())
			{
				LowLevelRequestResponseSerializer.Instance.Serialize(response, ms);
				return ms.ToArray();
			}
		}

		private static IDictionary<string, object> SniffResponseNodes(
			IEnumerable<Node> nodes,
			string elasticsearchVersion,
			string publishAddressOverride,
			bool randomFqdn
		) =>
			(from node in nodes
				let id = string.IsNullOrEmpty(node.Id) ? Guid.NewGuid().ToString("N").Substring(0, 8) : node.Id
				let name = string.IsNullOrEmpty(node.Name) ? Guid.NewGuid().ToString("N").Substring(0, 8) : node.Name
				select new { id, name, node })
			.ToDictionary(kv => kv.id, kv => CreateNodeResponse(kv.node, kv.name, elasticsearchVersion, publishAddressOverride, randomFqdn));

		private static object CreateNodeResponse(Node node, string name, string elasticsearchVersion, string publishAddressOverride, bool randomFqdn)
		{
			var port = node.Uri.Port;
			var fqdn = randomFqdn ? $"fqdn{port}/" : "";
			var host = !string.IsNullOrWhiteSpace(publishAddressOverride) ? publishAddressOverride : "127.0.0.1";

			var settings = new Dictionary<string, object>
			{
				{ "cluster.name", ClusterName },
				{ "node.name", name }
			};
			foreach (var kv in node.Settings) settings[kv.Key] = kv.Value;

			var httpEnabled = node.HasFeature(ElasticsearchNodeFeatures.HttpEnabled);

			var nodeResponse = new
			{
				name,
				settings,
				transport_address = $"127.0.0.1:{port + 1000}]",
				host = Guid.NewGuid().ToString("N").Substring(0, 8),
				ip = "127.0.0.1",
				version = elasticsearchVersion,
				build_hash = Guid.NewGuid().ToString("N").Substring(0, 8),
				roles = new List<string>(),
				http = httpEnabled
					? new
					{
						bound_address = new[]
						{
							$"{fqdn}127.0.0.1:{port}"
						},
						//publish_address = $"{fqdn}${publishAddress}"
						publish_address = $"{fqdn}{host}:{port}"
					}
					: null
			};
			if (node.HasFeature(ElasticsearchNodeFeatures.MasterEligible)) nodeResponse.roles.Add("master");
			if (node.HasFeature(ElasticsearchNodeFeatures.HoldsData)) nodeResponse.roles.Add("data");
			if (node.HasFeature(ElasticsearchNodeFeatures.IngestEnabled)) nodeResponse.roles.Add("ingest");
			if (!httpEnabled)
				nodeResponse.settings.Add("http.enabled", false);
			return nodeResponse;
		}
	}
}
