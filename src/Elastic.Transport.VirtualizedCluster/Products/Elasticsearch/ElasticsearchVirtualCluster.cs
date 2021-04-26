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
using System.Collections.ObjectModel;
using System.Linq;
using Elastic.Transport.Products.Elasticsearch;
using Elastic.Transport.VirtualizedCluster.Components;

namespace Elastic.Transport.VirtualizedCluster.Products.Elasticsearch
{
	/// <summary>
	/// Bootstrap an elasticsearch virtual cluster. This class can not be instantiated directly, use <see cref="Virtual.Elasticsearch" /> instead.
	/// </summary>
	public class ElasticsearchClusterFactory
	{
		internal static ElasticsearchClusterFactory Default { get; } = new ElasticsearchClusterFactory();

		private ElasticsearchClusterFactory() { }

		// ReSharper disable once MemberCanBeMadeStatic.Global
		/// <summary>
		/// Bootstrap a cluster with <paramref name="numberOfNodes"/>. By default all clusters start their nodes at 9200.
		/// <para>You can provide a different starting number using <paramref name="startFrom"/>.</para>
		/// </summary>
		public ElasticsearchVirtualCluster Bootstrap(int numberOfNodes, int startFrom = 9200) =>
			new ElasticsearchVirtualCluster(
				Enumerable.Range(startFrom, numberOfNodes).Select(n => new Node(new Uri($"http://localhost:{n}")))
			);

		// ReSharper disable once MemberCanBeMadeStatic.Global
		/// <summary> Bootstrap a cluster by providing the nodes explicitly </summary>
		public ElasticsearchVirtualCluster Bootstrap(IEnumerable<Node> nodes) => new ElasticsearchVirtualCluster(nodes);

		// ReSharper disable once MemberCanBeMadeStatic.Global
		/// <summary>
		/// Bootstrap a cluster with <paramref name="numberOfNodes"/>. By default all clusters start their nodes at 9200.
		/// <para>You can provide a different starting number using <paramref name="startFrom"/>.</para>
		/// <para>Using this overload all the nodes in the cluster are ONLY master eligible</para>
		/// </summary>
		public ElasticsearchVirtualCluster BootstrapAllMasterEligableOnly(int numberOfNodes, int startFrom = 9200) =>
			new ElasticsearchVirtualCluster(
				Enumerable.Range(startFrom, numberOfNodes)
					.Select(n => new Node(new Uri($"http://localhost:{n}"), ElasticsearchNodeFeatures.MasterEligibleOnly)
					)
			);
	}

	/// <summary>
	/// Create a virtual Elasticsearch cluster by passing a list of <see cref="Node"/>.
	/// <para>Please see <see cref="Virtual.Elasticsearch" /> for a more convenient pattern to create an instance of this class.</para>
	/// </summary>
	public class ElasticsearchVirtualCluster : VirtualCluster
	{
		/// <inheritdoc cref="ElasticsearchVirtualCluster"/>>
		public ElasticsearchVirtualCluster(IEnumerable<Node> nodes) : base(nodes, ElasticsearchMockProductRegistration.Default) { }

		/// <summary>
		/// Makes sure **only** the nodes with the passed port numbers are marked as master eligible. By default **all** nodes
		/// are master eligible.
		/// </summary>
		public ElasticsearchVirtualCluster MasterEligible(params int[] ports)
		{
			foreach (var node in InternalNodes.Where(n => !ports.Contains(n.Uri.Port)))
			{
				var currentFeatures = node.Features.Count == 0 ? ElasticsearchNodeFeatures.Default : node.Features;
				node.Features = currentFeatures.Except(new[] { ElasticsearchNodeFeatures.MasterEligible }).ToList().AsReadOnly();
			}
			return this;
		}

		/// <summary> Removes the data role from the nodes with the passed port numbers </summary>
		public ElasticsearchVirtualCluster StoresNoData(params int[] ports)
		{
			foreach (var node in InternalNodes.Where(n => ports.Contains(n.Uri.Port)))
			{
				var currentFeatures = node.Features.Count == 0 ? ElasticsearchNodeFeatures.Default : node.Features;
				node.Features = currentFeatures.Except(new[] { ElasticsearchNodeFeatures.HoldsData }).ToList().AsReadOnly();
			}
			return this;
		}

		/// <summary> Disables http on the nodes with the passed port numbers </summary>
		public VirtualCluster HttpDisabled(params int[] ports)
		{
			foreach (var node in InternalNodes.Where(n => ports.Contains(n.Uri.Port)))
			{
				var currentFeatures = node.Features.Count == 0 ? ElasticsearchNodeFeatures.Default : node.Features;
				node.Features = currentFeatures.Except(new[] { ElasticsearchNodeFeatures.HttpEnabled }).ToList().AsReadOnly();
			}
			return this;
		}

		/// <summary> Add a setting to the nodes with the passed port numbers </summary>
		public ElasticsearchVirtualCluster HasSetting(string key, string value, params int[] ports)
		{
			foreach (var node in InternalNodes.Where(n => ports.Contains(n.Uri.Port)))
				node.Settings = new ReadOnlyDictionary<string, object>(new Dictionary<string, object> { { key, value } });
			return this;
		}


	}
}
