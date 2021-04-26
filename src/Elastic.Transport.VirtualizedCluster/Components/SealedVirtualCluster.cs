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
using Elastic.Transport.VirtualizedCluster.Products;
using Elastic.Transport.VirtualizedCluster.Providers;

namespace Elastic.Transport.VirtualizedCluster.Components
{
	/// <summary>
	/// A continuation of <see cref="VirtualCluster"/>'s builder methods that creates
	/// an instance of <see cref="TransportConfiguration"/> for the cluster after which the components such as
	/// <see cref="IConnection"/> and <see cref="IConnectionPool"/> can no longer be updated.
	/// </summary>
	public class SealedVirtualCluster
	{
		private readonly IConnection _connection;
		private readonly IConnectionPool _connectionPool;
		private readonly TestableDateTimeProvider _dateTimeProvider;
		private readonly IMockProductRegistration _productRegistration;

		internal SealedVirtualCluster(VirtualCluster cluster, IConnectionPool pool, TestableDateTimeProvider dateTimeProvider, IMockProductRegistration productRegistration)
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
