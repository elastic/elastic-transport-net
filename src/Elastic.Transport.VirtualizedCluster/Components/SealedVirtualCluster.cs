// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using Elastic.Transport.VirtualizedCluster.Products;
using Elastic.Transport.VirtualizedCluster.Providers;

namespace Elastic.Transport.VirtualizedCluster.Components;

/// <summary>
/// A continuation of <see cref="VirtualCluster"/>'s builder methods that creates
/// an instance of <see cref="TransportConfigurationDescriptor"/> for the cluster after which the components such as
/// <see cref="IRequestInvoker"/> and <see cref="NodePool"/> can no longer be updated.
/// </summary>
public sealed class SealedVirtualCluster
{
	private readonly IRequestInvoker _requestInvoker;
	private readonly NodePool _nodePool;
	private readonly MockProductRegistration _productRegistration;

	internal SealedVirtualCluster(VirtualCluster cluster, NodePool pool, TestableDateTimeProvider dateTimeProvider, MockProductRegistration productRegistration)
	{
		_nodePool = pool;
		_requestInvoker = new VirtualClusterRequestInvoker(cluster, dateTimeProvider);
		_productRegistration = productRegistration;
	}

	private TransportConfigurationDescriptor CreateSettings() =>
		new(_nodePool, _requestInvoker, serializer: null, _productRegistration.ProductRegistration);


	/// <summary> Create the cluster using all defaults on <see cref="TransportConfigurationDescriptor"/> </summary>
	public VirtualizedCluster AllDefaults() =>
		new(CreateSettings());

	/// <summary> Create the cluster using <paramref name="selector"/> to provide configuration changes </summary>
	/// <param name="selector">Provide custom configuration options</param>
	public VirtualizedCluster Settings(Func<TransportConfigurationDescriptor, TransportConfigurationDescriptor> selector) =>
		new(selector(CreateSettings()));

	/// <summary>
	/// Allows you to create an instance of `<see cref="VirtualClusterConnection"/> using the DSL provided by <see cref="Virtual"/>
	/// </summary>
	/// <param name="selector">Provide custom configuration options</param>
	public VirtualClusterRequestInvoker VirtualClusterConnection(Func<TransportConfigurationDescriptor, TransportConfigurationDescriptor> selector = null) =>
		new VirtualizedCluster(selector == null ? CreateSettings() : selector(CreateSettings()))
			.Connection;
}
