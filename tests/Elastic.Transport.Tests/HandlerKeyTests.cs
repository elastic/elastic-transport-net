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

	[Fact]
	public void TheSameTimeoutReusesTheClient()
	{
		var settings = new TransportConfiguration(Node);
		using var factory = new BoundConfigurationHttpClientFactory(_ => new HttpClientHandler());

		var first = factory.CreateClient(new BoundConfiguration(settings));
		var second = factory.CreateClient(new BoundConfiguration(settings, new RequestConfiguration { MaxRetries = 1 }));

		_ = second.Should().BeSameAs(first);
	}

	[Fact]
	public void AnotherTimeoutGetsItsOwnClientOverTheSameHandlerAndKeepsItsTimeout()
	{
		var settings = new TransportConfiguration(Node);
		var created = 0;
		using var factory = new BoundConfigurationHttpClientFactory(_ =>
		{
			created++;
			return new HttpClientHandler();
		});

		var defaults = factory.CreateClient(new BoundConfiguration(settings));
		var shorter = factory.CreateClient(new BoundConfiguration(settings, new RequestConfiguration { RequestTimeout = TimeSpan.FromSeconds(3) }));
		var again = factory.CreateClient(new BoundConfiguration(settings));

		_ = shorter.Should().NotBeSameAs(defaults);
		_ = shorter.Timeout.Should().Be(TimeSpan.FromSeconds(3));
		_ = again.Should().BeSameAs(defaults, "the cached client still belongs to the first timeout");
		_ = created.Should().Be(1, "one handler, so one connection pool, whatever the timeout");
	}

	[Fact]
	public void ClientsForDifferentHandlersAreDifferent()
	{
		var one = new TransportConfiguration(Node) { ConnectionLimit = 5 };
		var other = new TransportConfiguration(Node) { ConnectionLimit = 50 };
		using var factory = new BoundConfigurationHttpClientFactory(_ => new HttpClientHandler());

		_ = factory.CreateClient(new BoundConfiguration(one)).Should().NotBeSameAs(factory.CreateClient(new BoundConfiguration(other)));
	}

	[Fact]
	public void ManyThreadsOnceTheClientIsCachedAllGetIt()
	{
		var settings = new TransportConfiguration(Node);
		using var factory = new BoundConfigurationHttpClientFactory(_ => new HttpClientHandler());
		var cached = factory.CreateClient(new BoundConfiguration(settings));
		var seen = new System.Collections.Concurrent.ConcurrentBag<HttpClient>();

		_ = System.Threading.Tasks.Parallel.For(0, 200, _ => seen.Add(factory.CreateClient(new BoundConfiguration(settings))));

		_ = seen.Should().OnlyContain(c => ReferenceEquals(c, cached));
	}

	[Fact]
	public void ARotatedHandlerNeverHandsOutTheOldClient()
	{
		// A short DNS refresh makes the handler entry expire, and the cached client goes with it.
		var settings = new TransportConfiguration(Node) { DnsRefreshTimeout = TimeSpan.FromMilliseconds(50) };
		using var factory = new BoundConfigurationHttpClientFactory(_ => new HttpClientHandler());
		var bound = new BoundConfiguration(settings);
		var original = factory.CreateClient(bound);

		var deadline = DateTime.UtcNow.AddSeconds(10);
		var current = original;
		while (ReferenceEquals(current, original) && DateTime.UtcNow < deadline)
		{
			System.Threading.Thread.Sleep(25);
			current = factory.CreateClient(bound);
		}

		_ = current.Should().NotBeSameAs(original, "the entry expired, so the next request gets a client over a new handler");
	}
}

#endif
