// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;

namespace Elastic.Transport.Products.Elasticsearch;

/// <summary>
/// Allows you to control how <see cref="ITransport{TConfiguration}"/> behaves and where/how it connects to Elasticsearch
/// </summary>
public record ElasticsearchConfiguration : TransportConfiguration
{
	/// <summary>
	/// Creates a new instance of <see cref="ElasticsearchConfiguration"/>
	/// </summary>
	/// <param name="uri">The root of the Elastic stack product node we want to connect to. Defaults to http://localhost:9200</param>
	public ElasticsearchConfiguration(Uri? uri = null)
		: base(new SingleNodePool(uri ?? new Uri("http://localhost:9200")), productRegistration: ElasticsearchProductRegistration.Default) { }

	/// <summary>
	/// Sets up the client to communicate to Elastic Cloud using <paramref name="cloudId"/>,
	/// <para><see cref="CloudNodePool"/> documentation for more information on how to get your Cloud ID</para>
	/// </summary>
	public ElasticsearchConfiguration(string cloudId, BasicAuthentication credentials)
		: base(new CloudNodePool(cloudId, credentials), productRegistration: ElasticsearchProductRegistration.Default) { }

	/// <summary>
	/// Sets up the client to communicate to Elastic Cloud using <paramref name="cloudId"/>,
	/// <para><see cref="CloudNodePool"/> documentation for more information on how to get your Cloud ID</para>
	/// </summary>
	public ElasticsearchConfiguration(string cloudId, ApiKey credentials)
		: base(new CloudNodePool(cloudId, credentials), productRegistration: ElasticsearchProductRegistration.Default) { }

	/// <summary> Sets up the client to communicate to Elastic Cloud.</summary>
	public ElasticsearchConfiguration(Uri cloudEndpoint, BasicAuthentication credentials)
		: base(new CloudNodePool(cloudEndpoint, credentials), productRegistration: ElasticsearchProductRegistration.Default) { }

	/// <summary> Sets up the client to communicate to Elastic Cloud. </summary>
	public ElasticsearchConfiguration(Uri cloudEndpoint, ApiKey credentials)
		: base(new CloudNodePool(cloudEndpoint, credentials), productRegistration: ElasticsearchProductRegistration.Default) { }

}
