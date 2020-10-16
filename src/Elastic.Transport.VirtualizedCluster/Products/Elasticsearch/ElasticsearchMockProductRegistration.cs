// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Collections.Generic;
using Elastic.Transport.Products;
using Elastic.Transport.Products.Elasticsearch;

namespace Elastic.Transport.VirtualizedCluster.Products.Elasticsearch
{
	/// <inheritdoc cref="IMockProductRegistration"/>>
	public class ElasticsearchMockProductRegistration : IMockProductRegistration
	{
		/// <summary> A static instance of <see cref="ElasticsearchMockProductRegistration"/> to reuse </summary>
		public static IMockProductRegistration Default { get; } = new ElasticsearchMockProductRegistration();

		/// <inheritdoc cref="IMockProductRegistration.ProductRegistration"/>>
		public IProductRegistration ProductRegistration { get; } = ElasticsearchProductRegistration.Default;

		/// <inheritdoc cref="IMockProductRegistration.CreateSniffResponseBytes"/>>
		public byte[] CreateSniffResponseBytes(IReadOnlyList<Node> nodes, string stackVersion, string publishAddressOverride, bool returnFullyQualifiedDomainNames) =>
			ElasticsearchSniffResponseFactory.Create(nodes, stackVersion, publishAddressOverride, returnFullyQualifiedDomainNames);
	}
}
