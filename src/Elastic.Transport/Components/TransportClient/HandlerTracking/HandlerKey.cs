// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Runtime.CompilerServices;

namespace Elastic.Transport;

/// <summary>
/// Identifies one <c>HttpMessageHandler</c>, and so one connection pool: two requests share a
/// handler exactly when this is equal. It lists everything the handler is built from and nothing else.
/// </summary>
/// <remarks>
/// This replaces an <see cref="int"/> hash. A hash can collide, so two configurations with different proxy credentials
/// could share a handler; it left out settings the handler is built from (<c>ConnectionLimit</c>, <c>DnsRefreshTimeout</c>,
/// certificates, TLS validation), so whichever request created the handler first won them; and it included
/// <c>RequestTimeout</c>, which the handler never reads (the timeout is set on the <c>HttpClient</c>), so every distinct
/// per-request timeout got its own connection pool and its own TLS handshakes.
/// </remarks>
internal readonly struct HandlerKey : IEquatable<HandlerKey>
{
	private readonly bool _httpCompression;
	private readonly string? _proxyAddress;
	private readonly string? _proxyUsername;
	private readonly string? _proxyPassword;
	private readonly bool _disableAutomaticProxyDetection;
	private readonly TimeSpan _dnsRefreshTimeout;

	// Compared by reference on purpose: the transport configuration is a record with dozens of properties, and the
	// certificate collection has no value equality worth paying for on every request.
	private readonly object? _connectionSettings;
	private readonly object? _clientCertificates;

	public HandlerKey(BoundConfiguration configuration)
	{
		_httpCompression = configuration.HttpCompression;
		_proxyAddress = configuration.ProxyAddress;
		_proxyUsername = configuration.ProxyUsername;
		_proxyPassword = configuration.ProxyPassword;
		_disableAutomaticProxyDetection = configuration.DisableAutomaticProxyDetection;
		_dnsRefreshTimeout = configuration.DnsRefreshTimeout;
		_connectionSettings = configuration.ConnectionSettings;
		_clientCertificates = configuration.ClientCertificates;
	}

	public bool Equals(HandlerKey other) =>
		_httpCompression == other._httpCompression
		&& _disableAutomaticProxyDetection == other._disableAutomaticProxyDetection
		&& _dnsRefreshTimeout == other._dnsRefreshTimeout
		&& string.Equals(_proxyAddress, other._proxyAddress, StringComparison.Ordinal)
		&& string.Equals(_proxyUsername, other._proxyUsername, StringComparison.Ordinal)
		&& string.Equals(_proxyPassword, other._proxyPassword, StringComparison.Ordinal)
		&& ReferenceEquals(_connectionSettings, other._connectionSettings)
		&& ReferenceEquals(_clientCertificates, other._clientCertificates);

	public override bool Equals(object? obj) => obj is HandlerKey other && Equals(other);

	public override int GetHashCode()
	{
		unchecked
		{
			var hash = _httpCompression ? 1 : 0;
			hash = (hash * 397) ^ (_disableAutomaticProxyDetection ? 1 : 0);
			hash = (hash * 397) ^ _dnsRefreshTimeout.GetHashCode();
			hash = (hash * 397) ^ (_proxyAddress?.GetHashCode() ?? 0);
			hash = (hash * 397) ^ (_proxyUsername?.GetHashCode() ?? 0);
			hash = (hash * 397) ^ (_proxyPassword?.GetHashCode() ?? 0);
			hash = (hash * 397) ^ RuntimeHelpers.GetHashCode(_connectionSettings);
			hash = (hash * 397) ^ RuntimeHelpers.GetHashCode(_clientCertificates);
			return hash;
		}
	}
}
