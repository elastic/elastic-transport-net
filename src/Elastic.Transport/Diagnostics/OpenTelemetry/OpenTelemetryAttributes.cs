namespace Elastic.Transport.Diagnostics;

/// <summary>
/// Defines custom, attribute names, but defined under OpenTelemetry semantic conventions.
/// </summary>
internal static class OpenTelemetryAttributes
{
	/// <summary>
	/// The name of the product library consuming the Elastic.Transport library.
	/// </summary>
	public const string ElasticTransportProductName = "elastic.transport.product.name";

	/// <summary>
	/// The informational version of the product library consuming the Elastic.Transport library.
	/// </summary>
	public const string ElasticTransportProductVersion = "elastic.transport.product.version";

	/// <summary>
	/// The informational version of the Elastic.Transport library.
	/// </summary>
	public const string ElasticTransportVersion = "elastic.transport.version";

	/// <summary>
	/// The URL for the implemented OpenTelemetry schema version for attributes added by the transport layer.
	/// </summary>
	public const string ElasticTransportSchemaVersion = "elastic.transport.schema_url";

	/// <summary>
	/// May be included by the Elasticsearch client to communicate the schema version it conforms to.
	/// </summary>
	public const string DbElasticsearchSchemaUrl = "db.elasticsearch.schema_url";

	/// <summary>
	/// The number of nodes attempted during a logical operation to Elasticsearch.
	/// </summary>
	public const string ElasticTransportAttemptedNodes = "elastic.transport.attempted_nodes";

	/// <summary>
	/// The measured milliseconds taken to prepare the HTTP request before sending it to the server.
	/// </summary>
	public const string ElasticTransportPrepareRequestMs = "elastic.transport.prepare_request_ms";

	/// <summary>
	/// The measured milliseconds taken to deserialize an HTTP response from the server.
	/// </summary>
	public const string ElasticTransportDeserializeResponseMs = "elastic.transport.deserialize_response_ms";

	/// <summary>
	/// The human-readable identifier for the cluster, usually retrieved from response headers.
	/// </summary>
	public const string DbElasticsearchClusterName = "db.elasticsearch.cluster.name";

	/// <summary>
	/// The identifier of the node/insatnce which handled a request, usually retrieved from response headers.
	/// </summary>
	public const string DbElasticsearchNodeName = "db.elasticsearch.node.name";
}
