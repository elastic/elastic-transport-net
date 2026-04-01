// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

namespace Elastic.Transport.Products.Elasticsearch;

/// <summary>
/// Internal interface for setting <see cref="ElasticsearchServerError"/> on response types.
/// <para>
/// The public <see cref="IElasticsearchResponse.ElasticsearchServerError"/> only exposes a getter.
/// This interface provides the internal setter used by builders and the error decorator.
/// </para>
/// </summary>
internal interface IElasticsearchResponseSetter
{
	ElasticsearchServerError? ElasticsearchServerError { set; }
}
