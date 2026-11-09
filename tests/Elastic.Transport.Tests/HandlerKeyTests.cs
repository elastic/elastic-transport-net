// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

// The HttpClient handler cache does not exist on .NET Framework, which uses HttpWebRequest.
#if !NETFRAMEWORK
using System;
using System.Net.Http;
using FluentAssertions;
using Xunit;

namespace Elastic.Transport.Tests;

/// <summary>Which requests share a connection pool.</summary>
public class HandlerKeyTests
{
	private static readonly Uri Node = new("http://localhost:9200");

	private static int HandlersCreatedFor(TransportConfiguration settings, params IRequestConfiguration[] requests)
	{
		var created = 0;
		using var factory = new BoundConfigurationHttpClientFactory(_ =>
		{
			created++;
			return new HttpClientHandler();
		});
		foreach (var request in requests)
			_ = factory.CreateClient(new BoundConfiguration(settings, request));
		return created;
	}

	[Fact]
	public void DifferentRequestTimeoutsShareOneConnectionPool()
	{
		var settings = new TransportConfiguration(Node);
		var created = HandlersCreatedFor(settings,
			new RequestConfiguration { RequestTimeout = TimeSpan.FromSeconds(1) },
			new RequestConfiguration { RequestTimeout = TimeSpan.FromSeconds(2) },
			new RequestConfiguration { RequestTimeout = TimeSpan.FromSeconds(3) },
			null);

		_ = created.Should().Be(1, "the handler never reads the request timeout; it is set on the HttpClient");
	}

	[Fact]
	public void TheTimeoutStillAppliesToTheClient()
	{
		var settings = new TransportConfiguration(Node);
		using var factory = new BoundConfigurationHttpClientFactory(_ => new HttpClientHandler());

		var client = factory.CreateClient(new BoundConfiguration(settings, new RequestConfiguration { RequestTimeout = TimeSpan.FromSeconds(7) }));

		_ = client.Timeout.Should().Be(TimeSpan.FromSeconds(7));
	}

	[Fact]
	public void DifferentProxyCredentialsNeverShareAHandler()
	{
		var a = new TransportConfiguration(Node) { ProxyAddress = "http://proxy:8080", ProxyUsername = "alice", ProxyPassword = "x" };
		var b = new TransportConfiguration(Node) { ProxyAddress = "http://proxy:8080", ProxyUsername = "bob", ProxyPassword = "x" };

		using var factory = new BoundConfigurationHttpClientFactory(_ => new HttpClientHandler());
		_ = factory.CreateClient(new BoundConfiguration(a));
		_ = factory.CreateClient(new BoundConfiguration(b));

		_ = factory.InUseHandlers.Should().Be(2);
	}

	[Fact]
	public void SettingsTheHandlerIsBuiltFromAreNotSilentlyShared()
	{
		// Same proxy, compression and timeouts: the old hash was equal. Different settings objects (and so a different
		// ConnectionLimit, DnsRefreshTimeout, certificate validation) must not get the first one's handler.
		var one = new TransportConfiguration(Node) { ConnectionLimit = 5 };
		var other = new TransportConfiguration(Node) { ConnectionLimit = 50 };

		using var factory = new BoundConfigurationHttpClientFactory(_ => new HttpClientHandler());
		_ = factory.CreateClient(new BoundConfiguration(one));
		_ = factory.CreateClient(new BoundConfiguration(other));

		_ = factory.InUseHandlers.Should().Be(2);
	}

	[Fact]
	public void TheSameConfigurationReusesItsHandler()
	{
		var settings = new TransportConfiguration(Node);
		_ = HandlersCreatedFor(settings, null, null, new RequestConfiguration { MaxRetries = 1 }).Should().Be(1);
	}
}

#endif
