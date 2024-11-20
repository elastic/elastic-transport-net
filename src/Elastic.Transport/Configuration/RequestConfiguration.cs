// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Security.Cryptography.X509Certificates;

namespace Elastic.Transport;

/// <inheritdoc cref="IRequestConfiguration"/>
public record RequestConfiguration : IRequestConfiguration
{
	/// <summary> The default request timeout. Defaults to 1 minute </summary>
	public static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromMinutes(10);

	/// <summary>The default ping timeout. Defaults to 2 seconds</summary>
	public static readonly TimeSpan DefaultPingTimeout = TimeSpan.FromSeconds(2);

	/// <summary> The default ping timeout when the connection is over HTTPS. Defaults to 5 seconds </summary>
	public static readonly TimeSpan DefaultPingTimeoutOnSsl = TimeSpan.FromSeconds(5);

	/// <summary> The default user-agent.</summary>
	public static readonly UserAgent DefaultUserAgent = UserAgent.Create("elastic-transport-net");

	/// <inheritdoc cref="IRequestConfiguration"/>
	public RequestConfiguration()
	{
	}

	/// <inheritdoc cref="IRequestConfiguration"/>
	public RequestConfiguration(IRequestConfiguration config)
	{
#if NET
		ArgumentNullException.ThrowIfNull(config);
#else
		if (config is null)
			throw new ArgumentNullException(nameof(config));
#endif

		Accept = config.Accept;
		AllowedStatusCodes = config.AllowedStatusCodes;
		Authentication = config.Authentication;
		ClientCertificates = (config.ClientCertificates is null) ? null : new X509CertificateCollection(config.ClientCertificates);
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
		ResponseBuilders = config.ResponseBuilders;
		UserAgent = config.UserAgent;
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
	public bool? DisableSniff { get; init; }

	/// <inheritdoc />
	public bool? HttpPipeliningEnabled { get; init; }

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
	public IReadOnlyCollection<IResponseBuilder> ResponseBuilders { get; init; } = [];

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

	/// <inheritdoc />
	public UserAgent? UserAgent { get; init; }
}
