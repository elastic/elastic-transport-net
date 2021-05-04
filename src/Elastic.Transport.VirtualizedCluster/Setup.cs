// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.Transport.VirtualizedCluster.Products.Elasticsearch;

namespace Elastic.Transport.VirtualizedCluster
{
	/// <summary>
	/// Static factory class that can be used to bootstrap virtual product clusters. E.g a cluster of virtual Elasticsearch nodes.
	/// </summary>
	public static class Virtual
	{
		/// <summary>
		/// Bootstrap a virtual Elasticsearch cluster using <see cref="ElasticsearchClusterFactory"/>
		/// </summary>
		public static ElasticsearchClusterFactory Elasticsearch { get; } = ElasticsearchClusterFactory.Default;
	}
}
