// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

#if !NETFRAMEWORK
using System;
using System.Security.Cryptography.X509Certificates;
using CsCheck;
using FluentAssertions;
using Xunit;

namespace Elastic.Transport.Properties.Tests;

/// <summary>
/// Which requests share an HTTP handler, and so a connection pool. Two requests share one exactly when everything the
/// handler is built from agrees; anything else about a request (its timeout, retries, headers) must make no difference.
/// </summary>
public class HandlerKeyProperties
{
	private static readonly X509CertificateCollection CertsA = new();
	private static readonly X509CertificateCollection CertsB = new();

	private sealed record RequestSpec(bool? Compression, int Certs, int? TimeoutSeconds, int? MaxRetries, string? OpaqueId)
	{
		public static readonly Gen<RequestSpec> Generator =
			from compression in Gen.Bool.Nullable()
			from certs in Gen.Int[0, 2]
			from timeout in Gen.Int[1, 120].Nullable()
			from retries in Gen.Int[0, 5].Nullable()
			from opaque in Gen.OneOf<string?>(null, "a", "b")
			select new RequestSpec(compression, certs, timeout, retries, opaque);

		public RequestConfiguration ToRequest() =>
			new()
			{
				EnableHttpCompression = Compression,
				ClientCertificates = Certs switch { 1 => CertsA, 2 => CertsB, _ => null },
				RequestTimeout = TimeoutSeconds is { } s ? TimeSpan.FromSeconds(s) : null,
				MaxRetries = MaxRetries,
				OpaqueId = OpaqueId,
			};

		/// <summary>What the handler is built from, the way a person would write it down: not the way HandlerKey does.</summary>
		public (bool Compression, int Certs) HandlerInputs => (Compression ?? true, Certs);
	}

	private static TransportConfiguration Settings(string? proxy, string? user, string? password, bool noAutoProxy, int dnsSeconds) =>
		new(new Uri("http://localhost:9200"))
		{
			ProxyAddress = proxy, ProxyUsername = user, ProxyPassword = password, DisableAutomaticProxyDetection = noAutoProxy,
			DnsRefreshTimeout = TimeSpan.FromSeconds(dnsSeconds),
		};

	private static readonly Gen<TransportConfiguration> AnySettings =
		from proxy in Gen.OneOf<string?>(null, "http://p1:8080", "http://p2:8080")
		from user in Gen.OneOf<string?>(null, "alice", "bob")
		from password in Gen.OneOf<string?>(null, "x", "y")
		from noAutoProxy in Gen.Bool
		from dns in Gen.Int[1, 600]
		select Settings(proxy, user, password, noAutoProxy, dns);

	[Fact]
	public void OnOneTransportRequestsShareAHandlerExactlyWhenCompressionAndCertificatesAgree() =>
		Gen.Select(AnySettings, RequestSpec.Generator, RequestSpec.Generator).Sample((settings, a, b) =>
		{
			var keyA = new HandlerKey(new BoundConfiguration(settings, a.ToRequest()));
			var keyB = new HandlerKey(new BoundConfiguration(settings, b.ToRequest()));

			_ = keyA.Equals(keyB).Should().Be(a.HandlerInputs == b.HandlerInputs, $"{a} vs {b}");
			if (keyA.Equals(keyB))
				_ = keyA.GetHashCode().Should().Be(keyB.GetHashCode(), "equal keys must hash equal");
		}, iter: 1000);

	[Fact]
	public void TheTimeoutRetriesAndHeadersOfARequestNeverChangeItsHandler() =>
		Gen.Select(AnySettings, RequestSpec.Generator).Sample((settings, spec) =>
		{
			var plain = new HandlerKey(new BoundConfiguration(settings, new RequestConfiguration { EnableHttpCompression = spec.Compression, ClientCertificates = spec.ToRequest().ClientCertificates }));
			var noisy = new HandlerKey(new BoundConfiguration(settings, spec.ToRequest()));

			_ = noisy.Should().Be(plain, "only compression and certificates are handler inputs");
		}, iter: 500);

	[Fact]
	public void TwoTransportsNeverShareAHandlerEvenWhenEveryFieldMatches() =>
		Gen.Select(Gen.OneOf<string?>(null, "http://p1:8080"), Gen.Int[1, 600]).Sample((proxy, dns) =>
		{
			var one = Settings(proxy, null, null, false, dns);
			var other = Settings(proxy, null, null, false, dns);

			_ = new HandlerKey(new BoundConfiguration(one)).Equals(new HandlerKey(new BoundConfiguration(other))).Should().BeFalse(
				"a handler is built from the transport's settings (connection limit, TLS callbacks), which are compared by reference");
		}, iter: 100);
}
#endif
