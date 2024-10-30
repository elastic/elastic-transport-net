// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Security.Cryptography.X509Certificates;
using Elastic.Transport.Diagnostics;
using Elastic.Transport.Diagnostics.Auditing;
using Elastic.Transport.Extensions;

namespace Elastic.Transport;

/// <summary>
/// Represents an endpoint in a transport request, encapsulating the HTTP method, path and query,
/// and the node to which the request is being sent.
/// </summary>
/// <remarks>
/// This class is used to construct the URI for the request based on the node's URI and the path and query.
/// An empty endpoint can be created using the <see cref="Empty"/> method as a default or placeholder instance.
/// </remarks>
public record Endpoint(HttpMethod Method, string PathAndQuery, Node Node)
{
	/// <summary>
	/// The <see cref="Uri" /> for the request.
	/// </summary>
	public Uri Uri { get; } = new Uri(Node.Uri, PathAndQuery);

	/// <summary> Represents an empty endpoint used as a default or placeholder instance of <see cref="Endpoint"/>. </summary>
	public static Endpoint Empty(HttpMethod method, string pathAndQuery) => new(method, pathAndQuery, EmptyNode);

	private static readonly Node EmptyNode = new Node(new Uri("http://empty.example"));

	/// <summary> Indicates whether the endpoint is an empty placeholder instance. </summary>
	public bool IsEmpty => Node == EmptyNode;

	/// <inheritdoc/>
	public override string ToString() => $"{Method.GetStringValue()} {Uri}";

}

/// <summary>
/// Where and how <see cref="IRequestInvoker.Request{TResponse}" /> should connect to.
/// <para>
/// Represents the cumulative configuration from <see cref="ITransportConfiguration" />
/// and <see cref="IRequestConfiguration" />.
/// </para>
/// </summary>
public sealed class RequestData
{
//TODO add xmldocs and clean up this class
#pragma warning disable 1591
	public const string DefaultMimeType = "application/json";
	public const string OpaqueIdHeader = "X-Opaque-Id";
	public const string RunAsSecurityHeader = "es-security-runas-user";

	public RequestData(
		HttpMethod method,
		string pathAndQuery,
		PostData? data,
		ITransportConfiguration global,
		IRequestConfiguration? local,
		CustomResponseBuilder? customResponseBuilder,
		MemoryStreamFactory memoryStreamFactory,
		OpenTelemetryData openTelemetryData
	)
	{
		OpenTelemetryData = openTelemetryData;
		CustomResponseBuilder = customResponseBuilder;
		ConnectionSettings = global;
		MemoryStreamFactory = memoryStreamFactory;
		Method = method;
		PostData = data;

		PathAndQuery = pathAndQuery;

		SkipDeserializationForStatusCodes = global.SkipDeserializationForStatusCodes;
		DnsRefreshTimeout = global.DnsRefreshTimeout;
		MetaHeaderProvider = global.MetaHeaderProvider;
		ProxyAddress = global.ProxyAddress;
		ProxyUsername = global.ProxyUsername;
		ProxyPassword = global.ProxyPassword;
		DisableAutomaticProxyDetection = global.DisableAutomaticProxyDetection;
		UserAgent = global.UserAgent;
		KeepAliveInterval = (int)(global.KeepAliveInterval?.TotalMilliseconds ?? 2000);
		KeepAliveTime = (int)(global.KeepAliveTime?.TotalMilliseconds ?? 2000);

		RunAs = local.RunAs ?? global.RunAs;

		DisableDirectStreaming = local?.DisableDirectStreaming ?? global.DisableDirectStreaming ?? false;
		if (data != null)
			data.DisableDirectStreaming = DisableDirectStreaming;

		Pipelined = local?.HttpPipeliningEnabled ?? global.HttpPipeliningEnabled ?? true;
		HttpCompression = global.EnableHttpCompression ?? local.EnableHttpCompression ?? true;
		ContentType = local?.ContentType ?? global.Accept ?? DefaultMimeType;
		Accept = local?.Accept ?? global.Accept ?? DefaultMimeType;
		ThrowExceptions = local?.ThrowExceptions ?? global.ThrowExceptions ?? false;
		RequestTimeout = local?.RequestTimeout ?? global.RequestTimeout ?? RequestConfiguration.DefaultRequestTimeout;
		RequestMetaData = local?.RequestMetaData?.Items ?? EmptyReadOnly<string, string>.Dictionary;
		AuthenticationHeader = local?.Authentication ?? global.Authentication;
		AllowedStatusCodes = local?.AllowedStatusCodes ?? EmptyReadOnly<int>.Collection;
		ClientCertificates = local?.ClientCertificates ?? global.ClientCertificates;
		TransferEncodingChunked = local?.TransferEncodingChunked ?? global.TransferEncodingChunked ?? false;
		TcpStats = local?.EnableTcpStats ?? global.EnableTcpStats ?? true;
		ThreadPoolStats = local?.EnableThreadPoolStats ?? global.EnableThreadPoolStats ?? true;
		ParseAllHeaders = local?.ParseAllHeaders ?? global.ParseAllHeaders ?? false;
		ResponseHeadersToParse = local is not null
			? new HeadersList(local.ResponseHeadersToParse, global.ResponseHeadersToParse)
			: global.ResponseHeadersToParse;
		PingTimeout =
			local?.PingTimeout
			?? global.PingTimeout
			?? (global.NodePool.UsingSsl ? RequestConfiguration.DefaultPingTimeoutOnSsl : RequestConfiguration.DefaultPingTimeout);

		if (global.Headers != null)
			Headers = new NameValueCollection(global.Headers);

		if (local?.Headers != null)
		{
			Headers ??= new NameValueCollection();
			foreach (var key in local.Headers.AllKeys)
				Headers[key] = local.Headers[key];
		}

		if (!string.IsNullOrEmpty(local?.OpaqueId))
		{
			Headers ??= new NameValueCollection();
			Headers.Add(OpaqueIdHeader, local.OpaqueId);
		}

	}

	public string Accept { get; }
	public IReadOnlyCollection<int> AllowedStatusCodes { get; }
	public AuthorizationHeader? AuthenticationHeader { get; }
	public X509CertificateCollection? ClientCertificates { get; }
	public ITransportConfiguration ConnectionSettings { get; }
	public CustomResponseBuilder? CustomResponseBuilder { get; }
	public HeadersList? ResponseHeadersToParse { get; }
	public NameValueCollection Headers { get; }
	public bool DisableDirectStreaming { get; }
	public bool ParseAllHeaders { get; }
	public bool DisableAutomaticProxyDetection { get; }
	public bool HttpCompression { get; }
	public bool MadeItToResponse { get; set; }
	public int KeepAliveInterval { get; }
	public int KeepAliveTime { get; }
	public MemoryStreamFactory MemoryStreamFactory { get; }
	public HttpMethod Method { get; }

	public AuditEvent OnFailureAuditEvent => MadeItToResponse ? AuditEvent.BadResponse : AuditEvent.BadRequest;
	public PipelineFailure OnFailurePipelineFailure => MadeItToResponse ? PipelineFailure.BadResponse : PipelineFailure.BadRequest;
	public string PathAndQuery { get; }
	public TimeSpan PingTimeout { get; }
	public bool Pipelined { get; }
	public PostData? PostData { get; }
	public string ProxyAddress { get; }
	public string ProxyPassword { get; }
	public string ProxyUsername { get; }
	public string ContentType { get; }
	public TimeSpan RequestTimeout { get; }
	public string? RunAs { get; }
	public IReadOnlyCollection<int> SkipDeserializationForStatusCodes { get; }
	public bool ThrowExceptions { get; }
	public UserAgent UserAgent { get; }
	public bool TransferEncodingChunked { get; }
	public bool TcpStats { get; }
	public bool ThreadPoolStats { get; }

	public TimeSpan DnsRefreshTimeout { get; }

	public MetaHeaderProvider MetaHeaderProvider { get; }

	public IReadOnlyDictionary<string, string> RequestMetaData { get; }

	public bool IsAsync { get; internal set; }

	internal OpenTelemetryData OpenTelemetryData { get; }

	public override string ToString() => $"{Method.GetStringValue()} {PathAndQuery}";

	internal bool ValidateResponseContentType(string responseMimeType)
	{
		if (string.IsNullOrEmpty(responseMimeType)) return false;

		if (Accept == responseMimeType)
			return true;

		// TODO - Performance: Review options to avoid the replace here and compare more efficiently.
		var trimmedAccept = Accept.Replace(" ", "");
		var trimmedResponseMimeType = responseMimeType.Replace(" ", "");

		return trimmedResponseMimeType.Equals(trimmedAccept, StringComparison.OrdinalIgnoreCase)
			|| trimmedResponseMimeType.StartsWith(trimmedAccept, StringComparison.OrdinalIgnoreCase)

			// ES specific fallback required because:
			// - 404 responses from ES8 don't include the vendored header
			// - ES8 EQL responses don't include vendored type

			|| trimmedAccept.Contains("application/vnd.elasticsearch+json") && trimmedResponseMimeType.StartsWith(DefaultMimeType, StringComparison.OrdinalIgnoreCase);
	}

#pragma warning restore 1591
}
