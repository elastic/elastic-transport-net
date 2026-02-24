// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Text;

namespace Elastic.Transport;

/// <summary>
/// Specifies which Elastic Cloud service to target when using a Cloud ID.
/// </summary>
public enum CloudService
{
	/// <summary> Target the Elasticsearch cluster (default). </summary>
	Elasticsearch,
	/// <summary> Target the Kibana instance. </summary>
	Kibana,
}

/// <summary>
/// An <see cref="NodePool"/> implementation that can be seeded with a cloud id
/// and will signal the right defaults for the client to use for Elastic Cloud to <see cref="ITransportConfiguration"/>.
///
/// <para>Read more about Elastic Cloud Id:</para>
/// <para>https://www.elastic.co/guide/en/cloud/current/ec-cloud-id.html</para>
/// </summary>
public sealed class CloudNodePool : SingleNodePool
{
	private readonly record struct ParsedCloudId(string ClusterName, Uri ElasticsearchUri, Uri? KibanaUri);

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
	/// <para>* Host Name (mandatory, optionally with :port, defaults to 443)</para>
	/// <para>* Elasticsearch UUID (mandatory, optionally with :port)</para>
	/// <para>* Kibana UUID (optionally with :port)</para>
	/// <para>* APM UUID</para>
	/// <para></para>
	/// <para> We then use these tokens to create the URI to your Elastic Cloud cluster!</para>
	/// <para></para>
	/// <para> Read more here: https://www.elastic.co/guide/en/cloud/current/ec-cloud-id.html</para>
	/// </param>
	/// <param name="credentials">The credentials to use for authentication.</param>
	public CloudNodePool(string cloudId, AuthorizationHeader credentials)
		: this(ParseCloudId(cloudId), CloudService.Elasticsearch) =>
		AuthenticationHeader = credentials;

	/// <inheritdoc cref="CloudNodePool(string, AuthorizationHeader)"/>
	/// <param name="cloudId"><inheritdoc cref="CloudNodePool(string, AuthorizationHeader)" path="/param[@name='cloudId']"/></param>
	/// <param name="credentials"><inheritdoc cref="CloudNodePool(string, AuthorizationHeader)" path="/param[@name='credentials']"/></param>
	/// <param name="service">Which cloud service to target. Defaults to <see cref="CloudService.Elasticsearch"/>.</param>
	public CloudNodePool(string cloudId, AuthorizationHeader credentials, CloudService service)
		: this(ParseCloudId(cloudId), service) =>
		AuthenticationHeader = credentials;

	/// <summary>
	/// An <see cref="NodePool"/> implementation that can be seeded with a cloud endpoint
	/// and will signal the right defaults for the client to use for Elastic Cloud to <see cref="ITransportConfiguration"/>.
	/// </summary>
	/// <param name="cloudEndpoint">Elastic Cloud endpoint</param>
	/// <param name="credentials">The credentials to use with cloud</param>
	public CloudNodePool(Uri cloudEndpoint, AuthorizationHeader credentials) : this(CreateCloudId(cloudEndpoint), CloudService.Elasticsearch) =>
		AuthenticationHeader = credentials;

	private CloudNodePool(ParsedCloudId parsedCloudId, CloudService service) : base(ResolveUri(parsedCloudId, service)) =>
		ClusterName = parsedCloudId.ClusterName;

	private static Uri ResolveUri(ParsedCloudId parsed, CloudService service)
	{
		if (service == CloudService.Kibana)
		{
			if (parsed.KibanaUri is null)
				throw new ArgumentException("The cloud ID does not contain a Kibana UUID. Cannot target Kibana.");
			return parsed.KibanaUri;
		}

		return parsed.ElasticsearchUri;
	}

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
		return new ParsedCloudId(cloudId, uri, null);
	}

	private static readonly char[] ColonSeparator = [':'];
	private static readonly char[] DollarSeparator = ['$'];

	private static (string Id, string Port) ExtractPortFromId(string input, string defaultPort = "443")
	{
		var colonIndex = input.IndexOf(':');
		if (colonIndex < 0)
			return (input, defaultPort);
		return (input[..colonIndex], input[(colonIndex + 1)..]);
	}

	private static ParsedCloudId ParseCloudId(string cloudId)
	{
		const string exceptionSuffix = "should be a string in the form of cluster_name:base_64_data";
		if (string.IsNullOrWhiteSpace(cloudId))
			throw new ArgumentException($"Parameter {nameof(cloudId)} was null or empty but {exceptionSuffix}", nameof(cloudId));

		var tokens = cloudId.Split(ColonSeparator, 2);
		if (tokens.Length != 2)
			throw new ArgumentException($"Parameter {nameof(cloudId)} not in expected format, {exceptionSuffix}", nameof(cloudId));

		var clusterName = tokens[0];
		var encoded = tokens[1];
		if (string.IsNullOrWhiteSpace(encoded))
			throw new ArgumentException($"Parameter {nameof(cloudId)} base_64_data is empty, {exceptionSuffix}", nameof(cloudId));

		var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
		var parts = decoded.Split(DollarSeparator);
		if (parts.Length < 2)
			throw new ArgumentException($"Parameter {nameof(cloudId)} decoded base_64_data contains less then 2 tokens, {exceptionSuffix}", nameof(cloudId));

		var (host, defaultPort) = ExtractPortFromId(parts[0].Trim());
		if (string.IsNullOrWhiteSpace(host))
			throw new ArgumentException($"Parameter {nameof(cloudId)} decoded base_64_data contains no domain name, {exceptionSuffix}", nameof(cloudId));

		var (esId, esPort) = ExtractPortFromId(parts[1].Trim(), defaultPort);
		if (string.IsNullOrWhiteSpace(esId))
			throw new ArgumentException($"Parameter {nameof(cloudId)} decoded base_64_data contains no elasticsearch UUID, {exceptionSuffix}", nameof(cloudId));

		var esUri = BuildServiceUri(esId, host, esPort);

		Uri? kibanaUri = null;
		if (parts.Length >= 3)
		{
			var kibanaRaw = parts[2].Trim();
			if (!string.IsNullOrWhiteSpace(kibanaRaw))
			{
				var (kbId, kbPort) = ExtractPortFromId(kibanaRaw, defaultPort);
				if (!string.IsNullOrWhiteSpace(kbId))
					kibanaUri = BuildServiceUri(kbId, host, kbPort);
			}
		}

		return new ParsedCloudId(clusterName, esUri, kibanaUri);
	}

	private static Uri BuildServiceUri(string serviceId, string host, string port) =>
		port == "443"
			? new Uri($"https://{serviceId}.{host}")
			: new Uri($"https://{serviceId}.{host}:{port}");
}
