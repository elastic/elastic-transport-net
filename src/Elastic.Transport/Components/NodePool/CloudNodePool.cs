// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Text;

namespace Elastic.Transport;

/// <summary>
/// An <see cref="NodePool"/> implementation that can be seeded with a cloud id
/// and will signal the right defaults for the client to use for Elastic Cloud to <see cref="ITransportConfiguration"/>.
///
/// <para>Read more about Elastic Cloud Id:</para>
/// <para>https://www.elastic.co/guide/en/cloud/current/ec-cloud-id.html</para>
/// </summary>
public sealed class CloudNodePool : SingleNodePool
{
	private readonly record struct ParsedCloudId(string ClusterName, Uri Uri);

	/// <summary>
	/// An <see cref="NodePool"/> implementation that can be seeded with a cloud id
	/// and will signal the right defaults for the client to use for Elastic Cloud to <see cref="ITransportConfiguration"/>.
	///
	/// <para>Read more about Elastic Cloud Id here</para>
	/// <para>https://www.elastic.co/guide/en/cloud/current/ec-cloud-id.html</para>
	/// </summary>
	/// <param name="cloudId">
	/// The Cloud Id, this is available on your cluster's dashboard and is a string in the form of <code>cluster_name:base_64_encoded_string</code>
	/// <para>Base64 encoded string contains the following tokens in order separated by $:</para>
	/// <para>* Host Name (mandatory)</para>
	/// <para>* Elasticsearch UUID (mandatory)</para>
	/// <para>* Kibana UUID</para>
	/// <para>* APM UUID</para>
	/// <para></para>
	/// <para> We then use these tokens to create the URI to your Elastic Cloud cluster!</para>
	/// <para></para>
	/// <para> Read more here: https://www.elastic.co/guide/en/cloud/current/ec-cloud-id.html</para>
	/// </param>
	/// <param name="credentials"></param>
	public CloudNodePool(string cloudId, AuthorizationHeader credentials) : this(ParseCloudId(cloudId)) =>
		AuthenticationHeader  = credentials;

	/// <summary>
	/// An <see cref="NodePool"/> implementation that can be seeded with a cloud enpoint
	/// and will signal the right defaults for the client to use for Elastic Cloud to <see cref="ITransportConfiguration"/>.
	/// </summary>
	/// <param name="cloudEndpoint">Elastic Cloud endpoint</param>
	/// <param name="credentials">The credentials to use with cloud</param>
	public CloudNodePool(Uri cloudEndpoint, AuthorizationHeader credentials) : this(CreateCloudId(cloudEndpoint)) =>
		AuthenticationHeader  = credentials;

	private CloudNodePool(ParsedCloudId parsedCloudId) : base(parsedCloudId.Uri) =>
		ClusterName = parsedCloudId.ClusterName;

	//TODO implement debugger display for NodePool implementations and display it there and its ToString()
	// ReSharper disable once UnusedAutoPropertyAccessor.Local
	private string ClusterName { get; }

	/// <inheritdoc cref="AuthorizationHeader"/>
	// Initialized by all public constructors after calling the private constructor
	public AuthorizationHeader AuthenticationHeader { get; } = null!;

	private static ParsedCloudId CreateCloudId(Uri uri)
	{
		var moniker = $"{uri.Host}${Guid.NewGuid():N}";
		var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(moniker));
		var cloudId = $"name:{base64}";
		return new ParsedCloudId(cloudId, uri);

	}

	private static ParsedCloudId ParseCloudId(string cloudId)
	{
		const string exceptionSuffix = "should be a string in the form of cluster_name:base_64_data";
		if (string.IsNullOrWhiteSpace(cloudId))
			throw new ArgumentException($"Parameter {nameof(cloudId)} was null or empty but {exceptionSuffix}", nameof(cloudId));

		var tokens = cloudId.Split(new[] { ':' }, 2);
		if (tokens.Length != 2)
			throw new ArgumentException($"Parameter {nameof(cloudId)} not in expected format, {exceptionSuffix}", nameof(cloudId));

		var clusterName = tokens[0];
		var encoded = tokens[1];
		if (string.IsNullOrWhiteSpace(encoded))
			throw new ArgumentException($"Parameter {nameof(cloudId)} base_64_data is empty, {exceptionSuffix}", nameof(cloudId));

		var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
		var parts = decoded.Split(new[] { '$' });
		if (parts.Length < 2)
			throw new ArgumentException($"Parameter {nameof(cloudId)} decoded base_64_data contains less then 2 tokens, {exceptionSuffix}", nameof(cloudId));

		var domainName = parts[0].Trim();
		if (string.IsNullOrWhiteSpace(domainName))
			throw new ArgumentException($"Parameter {nameof(cloudId)} decoded base_64_data contains no domain name, {exceptionSuffix}", nameof(cloudId));

		var elasticsearchUuid = parts[1].Trim();
		if (string.IsNullOrWhiteSpace(elasticsearchUuid))
			throw new ArgumentException($"Parameter {nameof(cloudId)} decoded base_64_data contains no elasticsearch UUID, {exceptionSuffix}", nameof(cloudId));

		return new ParsedCloudId(clusterName, new Uri($"https://{elasticsearchUuid}.{domainName}"));
	}
}
