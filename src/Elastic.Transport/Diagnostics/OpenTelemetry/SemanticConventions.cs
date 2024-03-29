// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

namespace Elastic.Transport.Diagnostics;

/// <summary>
/// Constants for OpenTelemetrySemanticConventions
/// </summary>
internal static class SemanticConventions
{
	// DATABASE
	public const string DbSystem = "db.system";
	public const string DbUser = "db.user";

	// HTTP
	public const string HttpResponseStatusCode = "http.response.status_code";
	public const string HttpRequestMethod = "http.request.method";

	// SERVER
	public const string ServerAddress = "server.address";
	public const string ServerPort = "server.port";

	// URL
	public const string UrlFull = "url.full";

	// URL
	public const string UserAgentOriginal = "user_agent.original";
}
