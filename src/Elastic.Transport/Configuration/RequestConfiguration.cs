// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Security.Cryptography.X509Certificates;

using Elastic.Transport.Extensions;

namespace Elastic.Transport;

/// <summary>
/// Allows you to inject per request overrides to the current <see cref="ITransportConfiguration"/>.
/// </summary>
public interface IRequestConfiguration
{
	/// <summary>
	/// Force a different Accept header on the request
	/// </summary>
	string? Accept { get; }

	/// <summary>
	/// Treat the following statuses (on top of the 200 range) NOT as error.
	/// </summary>
	IReadOnlyCollection<int>? AllowedStatusCodes { get; }

	/// <summary> Provide an authentication header override for this request </summary>
	AuthorizationHeader? Authentication { get; }

	/// <summary>
	/// Use the following client certificates to authenticate this single request
	/// </summary>
	X509CertificateCollection? ClientCertificates { get; }

	/// <summary>
	/// Force a different Content-Type header on the request
	/// </summary>
	string? ContentType { get; }

	/// <summary>
	/// Whether to buffer the request and response bytes for the call
	/// </summary>
	bool? DisableDirectStreaming { get; }

	/// <summary>
	/// Whether to disable the audit trail for the request.
	/// </summary>
	bool? DisableAuditTrail { get; }

	/// <summary>
	/// Under no circumstance do a ping before the actual call. If a node was previously dead a small ping with
	/// low connect timeout will be tried first in normal circumstances
	/// </summary>
	bool? DisablePings { get; }

	/// <summary>
	/// Forces no sniffing to occur on the request no matter what configuration is in place
	/// globally
	/// </summary>
	bool? DisableSniff { get; }

	/// <summary>
	/// Whether or not this request should be pipelined. http://en.wikipedia.org/wiki/HTTP_pipelining defaults to true
	/// </summary>
	bool? HttpPipeliningEnabled { get; }

	/// <summary>
	/// Enable gzip compressed requests and responses
	/// </summary>
	bool? EnableHttpCompression { get; }

	/// <summary>
	/// This will force the operation on the specified node, this will bypass any configured connection pool and will no retry.
	/// </summary>
	Uri? ForceNode { get; }

	/// <summary>
	/// When a retryable exception occurs or status code is returned this controls the maximum
	/// amount of times we should retry the call to Elasticsearch
	/// </summary>
	int? MaxRetries { get; }

	/// <summary>
	/// Limits the total runtime including retries separately from <see cref="IRequestConfiguration.RequestTimeout" />
	/// <pre>
	/// When not specified defaults to <see cref="IRequestConfiguration.RequestTimeout" /> which itself defaults to 60 seconds
	/// </pre>
	/// </summary>
	TimeSpan? MaxRetryTimeout { get; }

	/// <summary>
	/// Associate an Id with this user-initiated task, such that it can be located in the cluster task list.
	/// Valid only for Elasticsearch 6.2.0+
	/// </summary>
	string? OpaqueId { get; }

	/// <summary> Determines whether to parse all HTTP headers in the request. </summary>
	bool? ParseAllHeaders { get; }

	/// <summary>
	/// The ping timeout for this specific request
	/// </summary>
	TimeSpan? PingTimeout { get; }

	/// <summary>
	/// The timeout for this specific request, takes precedence over the global timeout init
	/// </summary>
	TimeSpan? RequestTimeout { get; }

	/// <summary> Specifies the headers from the response that should be parsed. </summary>
	HeadersList? ResponseHeadersToParse { get; }

	/// <summary>
	/// Submit the request on behalf in the context of a different shield user
	/// <pre />https://www.elastic.co/guide/en/shield/current/submitting-requests-for-other-users.html
	/// </summary>
	string? RunAs { get; }

	/// <summary>
	/// Instead of following a c/go like error checking on response.IsValid do throw an exception (except when <see cref="ApiCallDetails.SuccessOrKnownError"/> is false)
	/// on the client when a call resulted in an exception on either the client or the Elasticsearch server.
	/// <para>Reasons for such exceptions could be search parser errors, index missing exceptions, etc...</para>
	/// </summary>
	bool? ThrowExceptions { get; }

	/// <summary>
	/// Whether the request should be sent with chunked Transfer-Encoding.
	/// </summary>
	bool? TransferEncodingChunked { get; }

	/// <summary>
	/// Try to send these headers for this single request
	/// </summary>
	NameValueCollection? Headers { get; }

	/// <summary>
	/// Enable statistics about TCP connections to be collected when making a request
	/// </summary>
	bool? EnableTcpStats { get; }

	/// <summary>
	/// Enable statistics about thread pools to be collected when making a request
	/// </summary>
	bool? EnableThreadPoolStats { get; }

	/// <summary>
	/// Holds additional meta data about the request.
	/// </summary>
	RequestMetaData? RequestMetaData { get; }
}

/// <inheritdoc cref="IRequestConfiguration"/>
public record RequestConfiguration : IRequestConfiguration
{
	/// <summary> The default request timeout. Defaults to 1 minute </summary>
	public static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromMinutes(10);

	/// <summary>The default ping timeout. Defaults to 2 seconds</summary>
	public static readonly TimeSpan DefaultPingTimeout = TimeSpan.FromSeconds(2);

	/// <summary> The default ping timeout when the connection is over HTTPS. Defaults to 5 seconds </summary>
	public static readonly TimeSpan DefaultPingTimeoutOnSsl = TimeSpan.FromSeconds(5);

	/// <inheritdoc cref="IRequestConfiguration"/>
	public RequestConfiguration()
	{
	}

	/// <inheritdoc cref="IRequestConfiguration"/>
	public RequestConfiguration(IRequestConfiguration config)
	{
#if NET8_0_OR_GREATER
		ArgumentNullException.ThrowIfNull(config);
#else
		if (config is null)
			throw new ArgumentNullException(nameof(config));
#endif

		Accept = config.Accept;
		AllowedStatusCodes = config.AllowedStatusCodes;
		Authentication = config.Authentication;
		ClientCertificates = config.ClientCertificates;
		ContentType = config.ContentType;
		DisableDirectStreaming = config.DisableDirectStreaming;
		DisableAuditTrail = config.DisableAuditTrail;
		DisablePings = config.DisablePings;
		DisableSniff = config.DisableSniff;
		HttpPipeliningEnabled = config.HttpPipeliningEnabled;
		EnableHttpCompression = config.EnableHttpCompression;
		ForceNode = config.ForceNode;
		MaxRetries = config.MaxRetries;
		MaxRetryTimeout = config.MaxRetryTimeout;
		OpaqueId = config.OpaqueId;
		PingTimeout = config.PingTimeout;
		RequestTimeout = config.RequestTimeout;
		RunAs = config.RunAs;
		ThrowExceptions = config.ThrowExceptions;
		TransferEncodingChunked = config.TransferEncodingChunked;
		Headers = (config.Headers is null) ? null : new NameValueCollection(config.Headers);
		EnableTcpStats = config.EnableTcpStats;
		EnableThreadPoolStats = config.EnableThreadPoolStats;
		ResponseHeadersToParse = (config.ResponseHeadersToParse is null) ? null : new HeadersList(config.ResponseHeadersToParse);
		ParseAllHeaders = config.ParseAllHeaders;
		RequestMetaData = config.RequestMetaData;
	}

	/// <inheritdoc />
	public string? Accept { get; init; }

	/// <inheritdoc />
	public IReadOnlyCollection<int>? AllowedStatusCodes { get; init; }

	/// <inheritdoc />
	public AuthorizationHeader? Authentication { get; init; }

	/// <inheritdoc />
	public X509CertificateCollection? ClientCertificates { get; init; }

	/// <inheritdoc />
	public string? ContentType { get; init; }

	/// <inheritdoc />
	public bool? DisableDirectStreaming { get; init; }

	/// <inheritdoc />
	public bool? DisableAuditTrail { get; init; }

	/// <inheritdoc />
	public bool? DisablePings { get; init; }

	/// <inheritdoc />
	public bool? DisablePing { get; init; } // TODO: ?

	/// <inheritdoc />
	public bool? DisableSniff { get; init; }

	/// <inheritdoc />
	public bool? HttpPipeliningEnabled { get; init; }

	/// <inheritdoc />
	public bool? EnableHttpPipelining { get; init; } = true; // TODO: ?

	/// <inheritdoc />
	public bool? EnableHttpCompression { get; init; }

	/// <inheritdoc />
	public Uri? ForceNode { get; init; }

	/// <inheritdoc />
	public int? MaxRetries { get; init; }

	/// <inheritdoc />
	public TimeSpan? MaxRetryTimeout { get; init; }

	/// <inheritdoc />
	public string? OpaqueId { get; init; }

	/// <inheritdoc />
	public TimeSpan? PingTimeout { get; init; }

	/// <inheritdoc />
	public TimeSpan? RequestTimeout { get; init; }

	/// <inheritdoc />
	public string? RunAs { get; init; }

	/// <inheritdoc />
	public bool? ThrowExceptions { get; init; }

	/// <inheritdoc />
	public bool? TransferEncodingChunked { get; init; }

	/// <inheritdoc />
	public NameValueCollection? Headers { get; init; }

	/// <inheritdoc />
	public bool? EnableTcpStats { get; init; }

	/// <inheritdoc />
	public bool? EnableThreadPoolStats { get; init; }

	/// <inheritdoc />
	public HeadersList? ResponseHeadersToParse { get; init; }

	/// <inheritdoc />
	public bool? ParseAllHeaders { get; init; }

	/// <inheritdoc />
	public RequestMetaData? RequestMetaData { get; init; }
}

/// <inheritdoc cref="IRequestConfiguration"/>
public class RequestConfigurationDescriptor : IRequestConfiguration
{
	/// <inheritdoc cref="IRequestConfiguration"/>
	public RequestConfigurationDescriptor() { }

	/// <inheritdoc cref="IRequestConfiguration"/>
	public RequestConfigurationDescriptor(IRequestConfiguration? config)
	{
		if (config is null)
			return;

		_accept = config.Accept;
		_allowedStatusCodes= config.AllowedStatusCodes;
		_authentication = config.Authentication;
		_clientCertificates = config.ClientCertificates;
		_contentType = config.ContentType;
		_disableDirectStreaming = config.DisableDirectStreaming;
		_disableAuditTrail = config.DisableAuditTrail;
		_disablePings = config.DisablePings;
		_disableSniff = config.DisableSniff;
		_httpPipeliningEnabled = config.HttpPipeliningEnabled;
		_enableHttpCompression = config.EnableHttpCompression;
		_forceNode = config.ForceNode;
		_maxRetries = config.MaxRetries;
		_maxRetryTimeout = config.MaxRetryTimeout;
		_opaqueId = config.OpaqueId;
		_pingTimeout = config.PingTimeout;
		_requestTimeout = config.RequestTimeout;
		_runAs = config.RunAs;
		_throwExceptions = config.ThrowExceptions;
		_transferEncodingChunked = config.TransferEncodingChunked;
		_headers = (config.Headers is null) ? null : new NameValueCollection(config.Headers);
		_enableTcpStats = config.EnableTcpStats;
		_enableThreadPoolStats = config.EnableThreadPoolStats;
		_responseHeadersToParse = (config.ResponseHeadersToParse is null) ? null : new HeadersList(config.ResponseHeadersToParse);
		_parseAllHeaders = config.ParseAllHeaders;
		_requestMetaData = config.RequestMetaData;
	}

	private string? _accept;
	private IReadOnlyCollection<int>? _allowedStatusCodes;
	private AuthorizationHeader? _authentication;
	private X509CertificateCollection? _clientCertificates;
	private string? _contentType;
	private bool? _disableDirectStreaming;
	private bool? _disableAuditTrail;
	private bool? _disablePings;
	private bool? _disableSniff;
	private bool? _httpPipeliningEnabled;
	private Uri? _forceNode;
	private int? _maxRetries;
	private string? _opaqueId;
	private bool? _parseAllHeaders;
	private TimeSpan? _pingTimeout;
	private TimeSpan? _requestTimeout;
	private HeadersList? _responseHeadersToParse;
	private string? _runAs;
	private bool? _throwExceptions;
	private bool? _transferEncodingChunked;
	private NameValueCollection? _headers;
	private bool? _enableTcpStats;
	private bool? _enableThreadPoolStats;
	private RequestMetaData? _requestMetaData;

#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
	private bool? _enableHttpCompression; // TODO: ?
	private TimeSpan? _maxRetryTimeout;   // TODO: ?
#pragma warning restore CS0649 // Field is never assigned to, and will always have its default value

	/// <inheritdoc cref="IRequestConfiguration.RunAs"/>
	public RequestConfigurationDescriptor RunAs(string username)
	{
		_runAs = username;
		return this;
	}

	/// <inheritdoc cref="IRequestConfiguration.RequestTimeout"/>
	public RequestConfigurationDescriptor RequestTimeout(TimeSpan requestTimeout)
	{
		_requestTimeout = requestTimeout;
		return this;
	}

	/// <inheritdoc cref="IRequestConfiguration.OpaqueId"/>
	public RequestConfigurationDescriptor OpaqueId(string opaqueId)
	{
		_opaqueId = opaqueId;
		return this;
	}

	/// <inheritdoc cref="IRequestConfiguration.PingTimeout"/>
	public RequestConfigurationDescriptor PingTimeout(TimeSpan pingTimeout)
	{
		_pingTimeout = pingTimeout;
		return this;
	}

	/// <inheritdoc cref="IRequestConfiguration.ContentType"/>
	public RequestConfigurationDescriptor ContentType(string contentTypeHeader)
	{
		_contentType = contentTypeHeader;
		return this;
	}

	/// <inheritdoc cref="IRequestConfiguration.Accept"/>
	public RequestConfigurationDescriptor Accept(string acceptHeader)
	{
		_accept = acceptHeader;
		return this;
	}

	/// <inheritdoc cref="IRequestConfiguration.AllowedStatusCodes"/>
	public RequestConfigurationDescriptor AllowedStatusCodes(IEnumerable<int>? codes)
	{
		_allowedStatusCodes = codes?.ToReadOnlyCollection();
		return this;
	}

	/// <inheritdoc cref="IRequestConfiguration.AllowedStatusCodes"/>
	public RequestConfigurationDescriptor AllowedStatusCodes(params int[] codes)
	{
		_allowedStatusCodes = codes.ToReadOnlyCollection();
		return this;
	}

	/// <inheritdoc cref="IRequestConfiguration.DisableSniff"/>
	public RequestConfigurationDescriptor DisableSniffing(bool disable = true)
	{
		_disableSniff = disable;
		return this;
	}

	/// <inheritdoc cref="IRequestConfiguration.DisablePings"/>
	public RequestConfigurationDescriptor DisablePing(bool disable = true)
	{
		_disablePings = disable;
		return this;
	}

	/// <inheritdoc cref="IRequestConfiguration.ThrowExceptions"/>
	public RequestConfigurationDescriptor ThrowExceptions(bool throwExceptions = true)
	{
		_throwExceptions = throwExceptions;
		return this;
	}

	/// <inheritdoc cref="IRequestConfiguration.DisableDirectStreaming"/>
	public RequestConfigurationDescriptor DisableDirectStreaming(bool disable = true)
	{
		_disableDirectStreaming = disable;
		return this;
	}

	/// <inheritdoc cref="IRequestConfiguration.DisableAuditTrail"/>
	public RequestConfigurationDescriptor DisableAuditTrail(bool disable = true)
	{
		_disableAuditTrail = disable;
		return this;
	}

	/// <inheritdoc cref="IRequestConfiguration.ForceNode"/>
	public RequestConfigurationDescriptor ForceNode(Uri uri)
	{
		_forceNode = uri;
		return this;
	}

	/// <inheritdoc cref="IRequestConfiguration.MaxRetries"/>
	public RequestConfigurationDescriptor MaxRetries(int retry)
	{
		_maxRetries = retry;
		return this;
	}

	/// <inheritdoc cref="AuthorizationHeader"/>
	public RequestConfigurationDescriptor Authentication(AuthorizationHeader authentication)
	{
		_authentication = authentication;
		return this;
	}

	/// <inheritdoc cref="IRequestConfiguration.HttpPipeliningEnabled"/>
	public RequestConfigurationDescriptor EnableHttpPipelining(bool enable = true)
	{
		_httpPipeliningEnabled = enable;
		return this;
	}

	/// <inheritdoc cref="IRequestConfiguration.ClientCertificates"/>
	public RequestConfigurationDescriptor ClientCertificates(X509CertificateCollection certificates)
	{
		_clientCertificates = certificates;
		return this;
	}

	/// <inheritdoc cref="IRequestConfiguration.ClientCertificates"/>
	public RequestConfigurationDescriptor ClientCertificate(X509Certificate certificate) =>
		ClientCertificates(new X509Certificate2Collection { certificate });

	/// <inheritdoc cref="IRequestConfiguration.ClientCertificates"/>
	public RequestConfigurationDescriptor ClientCertificate(string certificatePath) =>
		ClientCertificates(new X509Certificate2Collection { new X509Certificate(certificatePath) });

	/// <inheritdoc cref="IRequestConfiguration.TransferEncodingChunked" />
	public RequestConfigurationDescriptor TransferEncodingChunked(bool transferEncodingChunked = true)
	{
		_transferEncodingChunked = transferEncodingChunked;
		return this;
	}

	/// <inheritdoc cref="IRequestConfiguration.Headers" />
	public RequestConfigurationDescriptor GlobalHeaders(NameValueCollection headers)
	{
		_headers = headers;
		return this;
	}

	/// <inheritdoc cref="IRequestConfiguration.EnableTcpStats" />
	public RequestConfigurationDescriptor EnableTcpStats(bool enableTcpStats = true)
	{
		_enableTcpStats = enableTcpStats;
		return this;
	}

	/// <inheritdoc cref="IRequestConfiguration.EnableThreadPoolStats" />
	public RequestConfigurationDescriptor EnableThreadPoolStats(bool enableThreadPoolStats = true)
	{
		_enableThreadPoolStats = enableThreadPoolStats;
		return this;
	}

	/// <inheritdoc cref="IRequestConfiguration.ParseAllHeaders"/>
	public RequestConfigurationDescriptor ParseAllHeaders(bool enable = true)
	{
		_parseAllHeaders = enable;
		return this;
	}

	/// <inheritdoc cref="IRequestConfiguration.ResponseHeadersToParse"/>
	public RequestConfigurationDescriptor ResponseHeadersToParse(IEnumerable<string> headers)
	{
		_responseHeadersToParse = new HeadersList(headers);
		return this;
	}

	/// <inheritdoc cref="IRequestConfiguration.RequestMetaData" />
	public RequestConfigurationDescriptor RequestMetaData(RequestMetaData metaData)
	{
		_requestMetaData = metaData;
		return this;
	}

	string? IRequestConfiguration.Accept => _accept;

	IReadOnlyCollection<int>? IRequestConfiguration.AllowedStatusCodes => _allowedStatusCodes;

	AuthorizationHeader? IRequestConfiguration.Authentication => _authentication;

	X509CertificateCollection? IRequestConfiguration.ClientCertificates => _clientCertificates;

	string? IRequestConfiguration.ContentType => _contentType;

	bool? IRequestConfiguration.DisableDirectStreaming => _disableDirectStreaming;

	bool? IRequestConfiguration.DisableAuditTrail => _disableAuditTrail;

	bool? IRequestConfiguration.DisablePings => _disablePings;

	bool? IRequestConfiguration.DisableSniff => _disableSniff;

	bool? IRequestConfiguration.HttpPipeliningEnabled => _httpPipeliningEnabled;

	bool? IRequestConfiguration.EnableHttpCompression => _enableHttpCompression;

	Uri? IRequestConfiguration.ForceNode => _forceNode;

	int? IRequestConfiguration.MaxRetries => _maxRetries;

	TimeSpan? IRequestConfiguration.MaxRetryTimeout => _maxRetryTimeout;

	string? IRequestConfiguration.OpaqueId => _opaqueId;

	bool? IRequestConfiguration.ParseAllHeaders => _parseAllHeaders;

	TimeSpan? IRequestConfiguration.PingTimeout => _pingTimeout;

	TimeSpan? IRequestConfiguration.RequestTimeout => _requestTimeout;

	HeadersList? IRequestConfiguration.ResponseHeadersToParse => _responseHeadersToParse;

	string? IRequestConfiguration.RunAs => _runAs;

	bool? IRequestConfiguration.ThrowExceptions => _throwExceptions;

	bool? IRequestConfiguration.TransferEncodingChunked => _transferEncodingChunked;

	NameValueCollection? IRequestConfiguration.Headers => _headers;

	bool? IRequestConfiguration.EnableTcpStats => _enableTcpStats;

	bool? IRequestConfiguration.EnableThreadPoolStats => _enableThreadPoolStats;

	RequestMetaData? IRequestConfiguration.RequestMetaData => _requestMetaData;
}
