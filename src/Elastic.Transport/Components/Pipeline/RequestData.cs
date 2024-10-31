// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Elastic.Transport.Diagnostics;
using Elastic.Transport.Diagnostics.Auditing;
using Elastic.Transport.Extensions;

namespace Elastic.Transport;

/// <summary>
/// Where and how <see cref="IRequestInvoker.Request{TResponse}" /> should connect to.
/// <para>
/// Represents the cumulative configuration from <see cref="ITransportConfiguration" />
/// and <see cref="IRequestConfiguration" />.
/// </para>
/// </summary>
public sealed record RequestData
{
	private const string OpaqueIdHeader = "X-Opaque-Id";

	/// The default MIME type used for request and response payloads.
	public const string DefaultMimeType = "application/json";

	/// The security header used to run requests as a different user.
	public const string RunAsSecurityHeader = "es-security-runas-user";

	/// <inheritdoc cref="RequestData"/>
	public RequestData(
		ITransportConfiguration global,
		IRequestConfiguration? local,
		CustomResponseBuilder? customResponseBuilder
	)
	{
		CustomResponseBuilder = customResponseBuilder;
		ConnectionSettings = global;
		MemoryStreamFactory = global.MemoryStreamFactory;

		SkipDeserializationForStatusCodes = global.SkipDeserializationForStatusCodes ?? [];
		DnsRefreshTimeout = global.DnsRefreshTimeout;
		MetaHeaderProvider = global.MetaHeaderProvider;
		ProxyAddress = global.ProxyAddress;
		ProxyUsername = global.ProxyUsername;
		ProxyPassword = global.ProxyPassword;
		DisableAutomaticProxyDetection = global.DisableAutomaticProxyDetection;
		UserAgent = global.UserAgent;
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
		ContentType = local?.ContentType ?? global.Accept ?? DefaultMimeType;
		Accept = local?.Accept ?? global.Accept ?? DefaultMimeType;
		ThrowExceptions = local?.ThrowExceptions ?? global.ThrowExceptions ?? false;
		RequestTimeout = local?.RequestTimeout ?? global.RequestTimeout ?? RequestConfiguration.DefaultRequestTimeout;
		RequestMetaData = local?.RequestMetaData?.Items ?? EmptyReadOnly<string, string>.Dictionary;
		AuthenticationHeader = local?.Authentication ?? global.Authentication;
		AllowedStatusCodes = local?.AllowedStatusCodes ?? EmptyReadOnly<int>.Collection;
		ClientCertificates = local?.ClientCertificates ?? global.ClientCertificates;
		TransferEncodingChunked = local?.TransferEncodingChunked ?? global.TransferEncodingChunked ?? false;
		EnableTcpStats = local?.EnableTcpStats ?? global.EnableTcpStats ?? true;
		EnableThreadPoolStats = local?.EnableThreadPoolStats ?? global.EnableThreadPoolStats ?? true;
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
	/// <inheritdoc cref="ITransportConfiguration.UserAgent"/>
	public UserAgent UserAgent { get; }
	/// <inheritdoc cref="ITransportConfiguration.DnsRefreshTimeout"/>
	public TimeSpan DnsRefreshTimeout { get; }


	/// <inheritdoc cref="IRequestConfiguration.RequestMetaData"/>
	public IReadOnlyDictionary<string, string> RequestMetaData { get; }

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
	/// <inheritdoc cref="CustomResponseBuilder"/>
	public CustomResponseBuilder? CustomResponseBuilder { get; }
	/// <inheritdoc cref="IRequestConfiguration.ResponseHeadersToParse"/>
	public HeadersList? ResponseHeadersToParse { get; }
	/// <inheritdoc cref="IRequestConfiguration.Headers"/>
	public NameValueCollection Headers { get; }
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
	public bool DisablePings { get;  }

}
