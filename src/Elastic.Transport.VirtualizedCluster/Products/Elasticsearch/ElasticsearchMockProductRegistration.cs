// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using Elastic.Transport.Products;
using Elastic.Transport.Products.Elasticsearch;

namespace Elastic.Transport.VirtualizedCluster.Products.Elasticsearch;

/// <inheritdoc cref="MockProductRegistration"/>>
public sealed class ElasticsearchMockProductRegistration : MockProductRegistration
{
	/// <summary> A static instance of <see cref="ElasticsearchMockProductRegistration"/> to reuse </summary>
	public static MockProductRegistration Default { get; } = new ElasticsearchMockProductRegistration();

	/// <inheritdoc cref="MockProductRegistration.ProductRegistration"/>>
	public override ProductRegistration ProductRegistration { get; } = ElasticsearchProductRegistration.Default;

	/// <inheritdoc cref="MockProductRegistration.CreateSniffResponseBytes"/>>
	public override byte[] CreateSniffResponseBytes(IReadOnlyList<Node> nodes, string stackVersion, string publishAddressOverride, bool returnFullyQualifiedDomainNames) =>
		ElasticsearchSniffResponseFactory.Create(nodes, stackVersion, publishAddressOverride, returnFullyQualifiedDomainNames);

	public override bool IsSniffRequest(RequestData requestData) =>
		requestData.PathAndQuery.StartsWith(ElasticsearchProductRegistration.SniffPath, StringComparison.Ordinal);

	public override bool IsPingRequest(RequestData requestData) =>
		requestData.Method == HttpMethod.HEAD &&
		(requestData.PathAndQuery == string.Empty || requestData.PathAndQuery.StartsWith("?"));
}
