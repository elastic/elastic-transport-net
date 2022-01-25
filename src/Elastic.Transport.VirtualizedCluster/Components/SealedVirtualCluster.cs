// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using Elastic.Transport.VirtualizedCluster.Products;
using Elastic.Transport.VirtualizedCluster.Providers;

namespace Elastic.Transport.VirtualizedCluster.Components
{
	/// <summary>
	/// A continuation of <see cref="VirtualCluster"/>'s builder methods that creates
	/// an instance of <see cref="TransportConfiguration"/> for the cluster after which the components such as
	/// <see cref="ITransportClient"/> and <see cref="NodePool"/> can no longer be updated.
	/// </summary>
	public class SealedVirtualCluster
	{
		private readonly ITransportClient _connection;
		private readonly NodePool _connectionPool;
		private readonly TestableDateTimeProvider _dateTimeProvider;
		private readonly IMockProductRegistration _productRegistration;

		internal SealedVirtualCluster(VirtualCluster cluster, NodePool pool, TestableDateTimeProvider dateTimeProvider, IMockProductRegistration productRegistration)
		{
			_connectionPool = pool;
			_connection = new VirtualClusterConnection(cluster, dateTimeProvider);
			_dateTimeProvider = dateTimeProvider;
			_productRegistration = productRegistration;
		}

		private TransportConfiguration CreateSettings() =>
			new TransportConfiguration(_connectionPool, _connection, serializer: null, _productRegistration.ProductRegistration);

		/// <summary> Create the cluster using all defaults on <see cref="TransportConfiguration"/> </summary>
		public VirtualizedCluster AllDefaults() =>
			new VirtualizedCluster(_dateTimeProvider, CreateSettings());

		/// <summary> Create the cluster using <paramref name="selector"/> to provide configuration changes </summary>
		/// <param name="selector">Provide custom configuration options</param>
		public VirtualizedCluster Settings(Func<TransportConfiguration, TransportConfiguration> selector) =>
			new VirtualizedCluster(_dateTimeProvider, selector(CreateSettings()));

		/// <summary>
		/// Allows you to create an instance of `<see cref="VirtualClusterConnection"/> using the DSL provided by <see cref="Virtual"/>
		/// </summary>
		/// <param name="selector">Provide custom configuration options</param>
		public VirtualClusterConnection VirtualClusterConnection(Func<TransportConfiguration, TransportConfiguration> selector = null) =>
			new VirtualizedCluster(_dateTimeProvider, selector == null ? CreateSettings() : selector(CreateSettings()))
				.Connection;
	}
}
