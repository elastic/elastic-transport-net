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
/// Represents the cumulative configuration from <see cref="ITransportConfiguration" />
/// and <see cref="IRequestConfiguration" />.
/// </summary>
public sealed record BoundConfiguration : IRequestConfiguration
{
	private const string OpaqueIdHeader = "X-Opaque-Id";

	/// The default MIME type used for request and response payloads.
	public const string DefaultContentType = "application/json";

	/// The security header used to run requests as a different user.
	public const string RunAsSecurityHeader = "es-security-runas-user";

	/// <inheritdoc cref="BoundConfiguration"/>
	public BoundConfiguration(ITransportConfiguration global, IRequestConfiguration? local = null)
	{
		ConnectionSettings = global;
		MemoryStreamFactory = global.MemoryStreamFactory;
		SkipDeserializationForStatusCodes = global.SkipDeserializationForStatusCodes ?? [];
		DnsRefreshTimeout = global.DnsRefreshTimeout;
		MetaHeaderProvider = global.MetaHeaderProvider;
		ProxyAddress = global.ProxyAddress;
		ProxyUsername = global.ProxyUsername;
		ProxyPassword = global.ProxyPassword;
		DisableAutomaticProxyDetection = global.DisableAutomaticProxyDetection;
		UserAgent = global.UserAgent ?? local?.UserAgent ?? RequestConfiguration.DefaultUserAgent;
		KeepAliveInterval = (int)(global.KeepAliveInterval?.TotalMilliseconds ?? 2000);
		KeepAliveTime = (int)(global.KeepAliveTime?.TotalMilliseconds ?? 2000);
		RunAs = local?.RunAs ?? global.RunAs;
		DisableDirectStreaming = local?.DisableDirectStreaming ?? global.DisableDirectStreaming ?? false;
		ForceNode = global.ForceNode ?? local?.ForceNode;
		MaxRetries = ForceNode != null ? 0
			: Math.Min(global.MaxRetries.GetValueOrDefault(int.MaxValue), global.NodePool.MaxRetries);
		DisableSniff = global.DisableSniff ?? local?.DisableSniff ?? false;
		DisablePings = global.DisablePings ?? !global.NodePool.SupportsPinging;
		HttpPipeliningEnabled = local?.HttpPipeliningEnabled ?? global.HttpPipeliningEnabled ?? true;
		HttpCompression = global.EnableHttpCompression ?? local?.EnableHttpCompression ?? true;
		ContentType = local?.ContentType ?? global.Accept ?? DefaultContentType;
		Accept = local?.Accept ?? global.Accept ?? DefaultContentType;
		ThrowExceptions = local?.ThrowExceptions ?? global.ThrowExceptions ?? false;
		RequestTimeout = local?.RequestTimeout ?? global.RequestTimeout ?? RequestConfiguration.DefaultRequestTimeout;
		RequestMetaData = local?.RequestMetaData;
		AuthenticationHeader = local?.Authentication ?? global.Authentication;
		AllowedStatusCodes = local?.AllowedStatusCodes ?? EmptyReadOnly<int>.Collection;
		ClientCertificates = local?.ClientCertificates ?? global.ClientCertificates;
		TransferEncodingChunked = local?.TransferEncodingChunked ?? global.TransferEncodingChunked ?? false;
		EnableTcpStats = local?.EnableTcpStats ?? global.EnableTcpStats ?? false;
		EnableThreadPoolStats = local?.EnableThreadPoolStats ?? global.EnableThreadPoolStats ?? false;
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
			Headers ??= [];
			foreach (var key in local.Headers.AllKeys)
				Headers[key] = local.Headers[key];
		}

		OpaqueId = local?.OpaqueId;
		if (!string.IsNullOrEmpty(local?.OpaqueId))
		{
			Headers ??= [];
			Headers.Add(OpaqueIdHeader, local.OpaqueId);
		}

		// If there are builders set at the transport level and on the request config, we combine them,
		// prioritizing the request config response builders as most specific.
		if (local is not null && local.ResponseBuilders.Count > 0 && global.ResponseBuilders.Count > 0)
		{
			var builders = new IResponseBuilder[local.ResponseBuilders.Count + global.ResponseBuilders.Count];

			var counter = 0;
			foreach (var builder in local.ResponseBuilders)
				builders[counter++] = builder;
			foreach (var builder in global.ResponseBuilders)
				builders[counter++] = builder;

			ResponseBuilders = builders;
		}
		else if (local is not null && local.ResponseBuilders.Count > 0)
			ResponseBuilders = local.ResponseBuilders;
		else
			ResponseBuilders = global.ResponseBuilders;

		ProductResponseBuilders = global.ProductRegistration.ResponseBuilders;
		DisableAuditTrail = local?.DisableAuditTrail ?? global.DisableAuditTrail ?? false;
	}

	/// <inheritdoc cref="ITransportConfiguration.MemoryStreamFactory"/>
	public MemoryStreamFactory MemoryStreamFactory { get; }
	/// <inheritdoc cref="ITransportConfiguration.MetaHeaderProvider"/>
	public MetaHeaderProvider? MetaHeaderProvider { get; }
	/// <inheritdoc cref="ITransportConfiguration.DisableAutomaticProxyDetection"/>
	public bool DisableAutomaticProxyDetection { get; }
	/// <inheritdoc cref="ITransportConfiguration.KeepAliveInterval"/>
	public int KeepAliveInterval { get; }
	/// <inheritdoc cref="ITransportConfiguration.KeepAliveTime"/>
	public int KeepAliveTime { get; }
	/// <inheritdoc cref="ITransportConfiguration.ProxyAddress"/>
	public string? ProxyAddress { get; }
	/// <inheritdoc cref="ITransportConfiguration.ProxyPassword"/>
	public string? ProxyPassword { get; }
	/// <inheritdoc cref="ITransportConfiguration.ProxyUsername"/>
	public string? ProxyUsername { get; }
	/// <inheritdoc cref="ITransportConfiguration.SkipDeserializationForStatusCodes"/>
	public IReadOnlyCollection<int> SkipDeserializationForStatusCodes { get; }
	/// <inheritdoc cref="IRequestConfiguration.UserAgent"/>
	public UserAgent UserAgent { get; }
	/// <inheritdoc cref="ITransportConfiguration.DnsRefreshTimeout"/>
	public TimeSpan DnsRefreshTimeout { get; }
	/// <inheritdoc cref="IRequestConfiguration.RequestMetaData"/>
	public RequestMetaData? RequestMetaData { get; }
	/// <inheritdoc cref="IRequestConfiguration.Accept"/>
	public string Accept { get; }
	/// <inheritdoc cref="IRequestConfiguration.AllowedStatusCodes"/>
	public IReadOnlyCollection<int> AllowedStatusCodes { get; }
	/// <inheritdoc cref="IRequestConfiguration.Authentication"/>
	public AuthorizationHeader? AuthenticationHeader { get; }
	/// <inheritdoc cref="IRequestConfiguration.ClientCertificates"/>
	public X509CertificateCollection? ClientCertificates { get; }
	/// <inheritdoc cref="ITransportConfiguration"/>
	public ITransportConfiguration ConnectionSettings { get; }
	/// <inheritdoc cref="IRequestConfiguration.ResponseHeadersToParse"/>
	public HeadersList? ResponseHeadersToParse { get; }
	/// <inheritdoc cref="IRequestConfiguration.Headers"/>
	public NameValueCollection? Headers { get; }
	/// <inheritdoc cref="IRequestConfiguration.DisableDirectStreaming"/>
	public bool DisableDirectStreaming { get; }
	/// <inheritdoc cref="IRequestConfiguration.ParseAllHeaders"/>
	public bool ParseAllHeaders { get; }
	/// <inheritdoc cref="IRequestConfiguration.EnableHttpCompression"/>
	public bool HttpCompression { get; }
	/// <inheritdoc cref="IRequestConfiguration.ForceNode"/>
	public Uri? ForceNode { get; }
	/// <inheritdoc cref="IRequestConfiguration.PingTimeout"/>
	public TimeSpan PingTimeout { get; }
	/// <inheritdoc cref="IRequestConfiguration.HttpPipeliningEnabled"/>
	public bool HttpPipeliningEnabled { get; }
	/// <inheritdoc cref="IRequestConfiguration.ContentType"/>
	public string ContentType { get; }
	/// <inheritdoc cref="IRequestConfiguration.RequestTimeout"/>
	public TimeSpan RequestTimeout { get; }
	/// <inheritdoc cref="IRequestConfiguration.RunAs"/>
	public string? RunAs { get; }
	/// <inheritdoc cref="IRequestConfiguration.ThrowExceptions"/>
	public bool ThrowExceptions { get; }
	/// <inheritdoc cref="IRequestConfiguration.TransferEncodingChunked"/>
	public bool TransferEncodingChunked { get; }
	/// <inheritdoc cref="IRequestConfiguration.EnableTcpStats"/>
	public bool EnableTcpStats { get; }
	/// <inheritdoc cref="IRequestConfiguration.EnableThreadPoolStats"/>
	public bool EnableThreadPoolStats { get; }
	/// <inheritdoc cref="IRequestConfiguration.MaxRetries"/>
	public int MaxRetries { get; }
	/// <inheritdoc cref="IRequestConfiguration.DisableSniff"/>
	public bool DisableSniff { get; }
	/// <inheritdoc cref="IRequestConfiguration.DisablePings"/>
	public bool DisablePings { get; }
	/// <inheritdoc cref="IRequestConfiguration.ResponseBuilders"/>
	public IReadOnlyCollection<IResponseBuilder> ProductResponseBuilders { get; }
	/// <inheritdoc cref="IRequestConfiguration.ResponseBuilders"/>
	public IReadOnlyCollection<IResponseBuilder> ResponseBuilders { get; }
	/// <inheritdoc cref="IRequestConfiguration.DisableAuditTrail"/>
	public bool DisableAuditTrail { get; }
	/// <inheritdoc cref="IRequestConfiguration.OpaqueId"/>
	public string? OpaqueId { get; }

	string IRequestConfiguration.Accept => Accept;
	IReadOnlyCollection<int> IRequestConfiguration.AllowedStatusCodes => AllowedStatusCodes;
	AuthorizationHeader? IRequestConfiguration.Authentication => AuthenticationHeader;
	X509CertificateCollection? IRequestConfiguration.ClientCertificates => ClientCertificates;
	string IRequestConfiguration.ContentType => ContentType;
	bool? IRequestConfiguration.DisableDirectStreaming => DisableDirectStreaming;
	bool? IRequestConfiguration.DisableAuditTrail => DisableAuditTrail;
	bool? IRequestConfiguration.DisablePings => DisablePings;
	bool? IRequestConfiguration.DisableSniff => DisableSniff;
	bool? IRequestConfiguration.HttpPipeliningEnabled => HttpPipeliningEnabled;
	bool? IRequestConfiguration.EnableHttpCompression => HttpCompression;
	Uri? IRequestConfiguration.ForceNode => ForceNode;
	int? IRequestConfiguration.MaxRetries => MaxRetries;
	TimeSpan? IRequestConfiguration.MaxRetryTimeout => RequestTimeout;
	string? IRequestConfiguration.OpaqueId => OpaqueId;
	bool? IRequestConfiguration.ParseAllHeaders => ParseAllHeaders;
	TimeSpan? IRequestConfiguration.PingTimeout => PingTimeout;
	TimeSpan? IRequestConfiguration.RequestTimeout => RequestTimeout;
	IReadOnlyCollection<IResponseBuilder> IRequestConfiguration.ResponseBuilders => ResponseBuilders;
	HeadersList? IRequestConfiguration.ResponseHeadersToParse => ResponseHeadersToParse;
	string? IRequestConfiguration.RunAs => RunAs;
	bool? IRequestConfiguration.ThrowExceptions => ThrowExceptions;
	bool? IRequestConfiguration.TransferEncodingChunked => TransferEncodingChunked;
	NameValueCollection? IRequestConfiguration.Headers => Headers;
	bool? IRequestConfiguration.EnableTcpStats => EnableTcpStats;
	bool? IRequestConfiguration.EnableThreadPoolStats => EnableThreadPoolStats;
	RequestMetaData? IRequestConfiguration.RequestMetaData => RequestMetaData;

	/// <summary>
	/// Create a cachable instance of <see cref="BoundConfiguration"/> for use in high-performance scenarios.
	/// </summary>
	/// <param name="transport">An existing <see cref="ITransport{TConfiguration}"/> from which to bind transport configuration.</param>
	/// <param name="requestConfiguration">A request specific <see cref="IRequestConfiguration"/>.</param>
	/// <returns></returns>
	public static BoundConfiguration Create(ITransport<ITransportConfiguration> transport, IRequestConfiguration requestConfiguration) =>
		new(transport.Configuration, requestConfiguration);
}
