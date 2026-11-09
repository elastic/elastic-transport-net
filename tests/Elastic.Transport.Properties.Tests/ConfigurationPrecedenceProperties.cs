// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Linq;
using CsCheck;
using FluentAssertions;
using Xunit;

namespace Elastic.Transport.Properties.Tests;

/// <summary>
/// Every setting a request can override resolves to <c>request ?? transport ?? default</c>, in that order, for every
/// combination of which layer sets what. This is the rule that was wrong for six settings (it was transport-first, or
/// ignored the request) before 2.0.
/// </summary>
public class ConfigurationPrecedenceProperties
{
	private static readonly Uri[] Forced = [new("http://forced-a:9200"), new("http://forced-b:9200")];

	/// <summary>One layer's choices: null means "not set at this layer".</summary>
	private sealed record Layer(
		bool? DisableSniff, bool? DisablePings, bool? Compression, bool? ThrowExceptions, bool? DirectStreamingOff, bool? Pipelining,
		int? MaxRetries, int? RequestTimeoutSeconds, int? MaxRetryTimeoutSeconds, int? PingTimeoutSeconds, int? ForcedNode)
	{
		public static readonly Gen<Layer> Generator =
			from sniff in Gen.Bool.Nullable()
			from pings in Gen.Bool.Nullable()
			from compression in Gen.Bool.Nullable()
			from throws in Gen.Bool.Nullable()
			from direct in Gen.Bool.Nullable()
			from pipelining in Gen.Bool.Nullable()
			from retries in Gen.Int[0, 10].Nullable()
			from timeout in Gen.Int[1, 120].Nullable()
			from retryTimeout in Gen.Int[1, 240].Nullable()
			from pingTimeout in Gen.Int[1, 30].Nullable()
			from forced in Gen.Int[0, 1].Nullable()
			select new Layer(sniff, pings, compression, throws, direct, pipelining, retries, timeout, retryTimeout, pingTimeout, forced);

		private static TimeSpan? Seconds(int? s) => s is { } v ? TimeSpan.FromSeconds(v) : null;

		public TransportConfiguration AsTransport(NodePool pool) =>
			new(pool, new InMemoryRequestInvoker())
			{
				DisableSniff = DisableSniff, DisablePings = DisablePings, EnableHttpCompression = Compression, ThrowExceptions = ThrowExceptions,
				DisableDirectStreaming = DirectStreamingOff, HttpPipeliningEnabled = Pipelining, MaxRetries = MaxRetries,
				RequestTimeout = Seconds(RequestTimeoutSeconds), MaxRetryTimeout = Seconds(MaxRetryTimeoutSeconds),
				PingTimeout = Seconds(PingTimeoutSeconds), ForceNode = ForcedNode is { } i ? Forced[i] : null,
			};

		public RequestConfiguration AsRequest() =>
			new()
			{
				DisableSniff = DisableSniff, DisablePings = DisablePings, EnableHttpCompression = Compression, ThrowExceptions = ThrowExceptions,
				DisableDirectStreaming = DirectStreamingOff, HttpPipeliningEnabled = Pipelining, MaxRetries = MaxRetries,
				RequestTimeout = Seconds(RequestTimeoutSeconds), MaxRetryTimeout = Seconds(MaxRetryTimeoutSeconds),
				PingTimeout = Seconds(PingTimeoutSeconds), ForceNode = ForcedNode is { } i ? Forced[i] : null,
			};
	}

	[Fact]
	public void EveryResolvedSettingIsRequestThenTransportThenDefault() =>
		Gen.Select(Layer.Generator, Layer.Generator).Sample((transport, request) =>
		{
			const int nodes = 4;
			var pool = new StaticNodePool(Enumerable.Range(0, nodes).Select(i => new Node(new Uri($"http://n{i}:9200"))), randomize: false);
			using var settings = transport.AsTransport(pool);

			var bound = new BoundConfiguration(settings, request.AsRequest());

			T Pick<T>(T? local, T? global, T fallback) where T : struct => local ?? global ?? fallback;
			var because = $"transport={transport}\nrequest={request}";

			_ = bound.DisableSniff.Should().Be(Pick(request.DisableSniff, transport.DisableSniff, false), because);
			_ = bound.DisablePings.Should().Be(Pick(request.DisablePings, transport.DisablePings, !pool.SupportsPinging), because);
			_ = bound.HttpCompression.Should().Be(Pick(request.Compression, transport.Compression, true), because);
			_ = bound.ThrowExceptions.Should().Be(Pick(request.ThrowExceptions, transport.ThrowExceptions, false), because);
			_ = bound.DisableDirectStreaming.Should().Be(Pick(request.DirectStreamingOff, transport.DirectStreamingOff, false), because);
			_ = bound.HttpPipeliningEnabled.Should().Be(Pick(request.Pipelining, transport.Pipelining, true), because);

			var requestTimeout = (request.RequestTimeoutSeconds ?? transport.RequestTimeoutSeconds) is { } s ? TimeSpan.FromSeconds(s) : RequestConfiguration.DefaultRequestTimeout;
			_ = bound.RequestTimeout.Should().Be(requestTimeout, because);
			_ = bound.MaxRetryTimeout.Should().Be(
				(request.MaxRetryTimeoutSeconds ?? transport.MaxRetryTimeoutSeconds) is { } r ? TimeSpan.FromSeconds(r) : requestTimeout, because);
			_ = bound.PingTimeout.Should().Be(
				(request.PingTimeoutSeconds ?? transport.PingTimeoutSeconds) is { } p ? TimeSpan.FromSeconds(p) : RequestConfiguration.DefaultPingTimeout, because);

			var forced = request.ForcedNode ?? transport.ForcedNode;
			_ = bound.ForceNode.Should().Be(forced is { } f ? Forced[f] : null, because);

			// A forced node is the only attempt; otherwise the budget is capped by the pool (nodes - 1).
			var expectedRetries = forced is not null ? 0 : Math.Min(request.MaxRetries ?? transport.MaxRetries ?? int.MaxValue, nodes - 1);
			_ = bound.MaxRetries.Should().Be(expectedRetries, because);
		}, iter: 500);
}
