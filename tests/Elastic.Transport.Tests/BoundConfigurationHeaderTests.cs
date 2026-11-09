// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Specialized;
using FluentAssertions;
using Xunit;

namespace Elastic.Transport.Tests;

public class BoundConfigurationHeaderTests
{
	private static TransportConfiguration SettingsWith(NameValueCollection headers) =>
		new(new Uri("http://localhost:9200")) { Headers = headers };

	private static NameValueCollection Global() => new() { { "X-Team", "search" }, { "X-Env", "prod" } };

	[Fact]
	public void RequestsThatAddNoHeadersSeeTheTransportHeaders()
	{
		var bound = new BoundConfiguration(SettingsWith(Global()), new RequestConfiguration { MaxRetries = 1 });

		_ = bound.Headers["X-Team"].Should().Be("search");
		_ = bound.Headers["X-Env"].Should().Be("prod");
	}

	[Fact]
	public void RequestHeadersOverrideAndAddWithoutTouchingTheTransportHeaders()
	{
		var snapshot = new NameValueCollection(Global());
		var local = new RequestConfiguration { Headers = new NameValueCollection { { "X-Env", "staging" }, { "X-Trace", "abc" } }, OpaqueId = "op-1" };

		var bound = new BoundConfiguration(SettingsWith(Global()), local, snapshot);

		_ = bound.Headers["X-Team"].Should().Be("search");
		_ = bound.Headers["X-Env"].Should().Be("staging");
		_ = bound.Headers["X-Trace"].Should().Be("abc");
		_ = bound.Headers["X-Opaque-Id"].Should().Be("op-1");
		_ = bound.Headers.Should().NotBeSameAs(snapshot);
		// The shared snapshot is exactly what it was: a bind that adds headers works on its own copy.
		_ = snapshot.AllKeys.Should().BeEquivalentTo(["X-Team", "X-Env"]);
		_ = snapshot["X-Env"].Should().Be("prod");
	}

	[Fact]
	public void RequestsThatAddNoHeadersShareOneSnapshotAndLaterChangesToTheOriginalDoNotLeakIn()
	{
		var original = Global();
		var snapshot = new NameValueCollection(original);
		var settings = SettingsWith(original);

		original["X-Late"] = "added after the transport was built";
		var first = new BoundConfiguration(settings, new RequestConfiguration { MaxRetries = 1 }, snapshot);
		var second = new BoundConfiguration(settings, new RequestConfiguration { MaxRetries = 2 }, snapshot);

		_ = first.Headers.Should().BeSameAs(second.Headers);
		_ = first.Headers["X-Late"].Should().BeNull();
	}

	[Fact]
	public void NoHeadersAnywhereMeansNoHeaders()
	{
		var bound = new BoundConfiguration(new TransportConfiguration(new Uri("http://localhost:9200")), new RequestConfiguration { MaxRetries = 1 });

		_ = bound.Headers.Should().BeNull();
	}
}
