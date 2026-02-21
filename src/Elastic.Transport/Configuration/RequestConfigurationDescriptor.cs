// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Security.Cryptography.X509Certificates;

using Elastic.Transport.Extensions;

namespace Elastic.Transport;

/// <inheritdoc cref="IRequestConfiguration"/>
public class RequestConfigurationDescriptor : IRequestConfiguration
{
	/// <inheritdoc cref="IRequestConfiguration"/>
	public RequestConfigurationDescriptor() { }

	/// <inheritdoc cref="IRequestConfiguration"/>
	public RequestConfigurationDescriptor(IRequestConfiguration config)
	{
#if NET
		ArgumentNullException.ThrowIfNull(config);
#else
		if (config is null)
			throw new ArgumentNullException(nameof(config));
#endif

		_accept = config.Accept;
		_allowedStatusCodes = config.AllowedStatusCodes;
		_authentication = config.Authentication;
		_clientCertificates = (config.ClientCertificates is null) ? null : new X509CertificateCollection(config.ClientCertificates);
		;
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
		_responseBuilders = [.. config.ResponseBuilders];
		_userAgent = config.UserAgent;
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
	private bool? _enableHttpCompression;
	private Uri? _forceNode;
	private int? _maxRetries;
	private TimeSpan? _maxRetryTimeout;
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
	private List<IResponseBuilder>? _responseBuilders;
	private UserAgent? _userAgent;

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

	/// <inheritdoc cref="IRequestConfiguration.MaxRetryTimeout"/>
	public RequestConfigurationDescriptor MaxRetries(TimeSpan? timeout)
	{
		_maxRetryTimeout = timeout;
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

	/// <inheritdoc cref="IRequestConfiguration.EnableHttpCompression"/>
	public RequestConfigurationDescriptor EnableHttpCompression(bool enable = true)
	{
		_enableHttpCompression = enable;
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
#if NET10_0_OR_GREATER
		ClientCertificates(new X509Certificate2Collection { X509CertificateLoader.LoadCertificateFromFile(certificatePath) });
#else
		ClientCertificates(new X509Certificate2Collection { new X509Certificate(certificatePath) });
#endif

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

	/// <inheritdoc cref="IRequestConfiguration.ResponseBuilders"/>
	public RequestConfigurationDescriptor ResponseBuilder(IResponseBuilder responseBuilder)
	{
		_responseBuilders ??= [];
		_responseBuilders.Add(responseBuilder);
		return this;
	}

	/// <inheritdoc cref="IRequestConfiguration.UserAgent" />
	public RequestConfigurationDescriptor UserAgent(UserAgent userAgent)
	{
		_userAgent = userAgent;
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

	IReadOnlyCollection<IResponseBuilder> IRequestConfiguration.ResponseBuilders => _responseBuilders ?? [];

	UserAgent? IRequestConfiguration.UserAgent => _userAgent;
}
