// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Elastic.Transport.IntegrationTests.Plumbing;
using Elastic.Transport.IntegrationTests.Plumbing.Stubs;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Elastic.Transport.IntegrationTests.Http;

[ApiController, Route("[controller]")]
public class ChunkedController : ControllerBase
{
	[HttpPost]
	public Task<JsonElement> Post([FromBody] JsonElement body) => Task.FromResult(body);
}

public class NonParallel { }
[CollectionDefinition(nameof(NonParallel), DisableParallelization = true)]
public class TransferEncodingChunkedTests(TestServerFixture instance) : AssemblyServerTestsBase(instance)
{
	private const string BodyString = /*lang=json,strict*/ "{\"query\":{\"match_all\":{}}}";
	private static readonly PostData Body = PostData.String(BodyString);
	private const string Path = "/chunked";

	private ITransport Setup(
		TrackingRequestInvoker requestInvoker,
		bool? disableAutomaticProxyDetection = null,
		bool httpCompression = false,
		bool transferEncodingChunked = false
	)
	{
		var nodePool = new SingleNodePool(Server.Uri);
		var config = new TransportConfiguration(nodePool, requestInvoker)
		{
			TransferEncodingChunked = transferEncodingChunked,
			EnableHttpCompression = httpCompression
		};

		config = disableAutomaticProxyDetection.HasValue
			? config with { DisableAutomaticProxyDetection = disableAutomaticProxyDetection.Value }
			: config;

		return new DistributedTransport(config);
	}

	/// <summary>
	/// Setting HttpClientHandler.Proxy = null don't disable HttpClient automatic proxy detection.
	/// It is disabled by setting Proxy to non-null value or by setting UseProxy = false.
	/// </summary>
	[Fact]
	public async Task HttpClientUseProxyShouldBeFalseWhenDisabledAutoProxyDetection()
	{
		var requestInvoker = new TrackingRequestInvoker();
		var transport = Setup(requestInvoker, disableAutomaticProxyDetection: true);

		var r = transport.Post<StringResponse>(Path, Body);
		_ = requestInvoker.LastHttpClientHandler.UseProxy.Should().BeFalse();
		_ = r.Body.Should().Be(BodyString);

		r = await transport.PostAsync<StringResponse>(Path, Body, CancellationToken.None);
		_ = requestInvoker.LastHttpClientHandler.UseProxy.Should().BeFalse();
		_ = r.Body.Should().Be(BodyString);
	}

	[Fact]
	public async Task HttpClientUseProxyShouldBeTrueWhenEnabledAutoProxyDetection()
	{
		var requestInvoker = new TrackingRequestInvoker();
		var transport = Setup(requestInvoker);

		_ = transport.Post<StringResponse>(Path, Body);
		_ = requestInvoker.LastHttpClientHandler.UseProxy.Should().BeTrue();
		_ = await transport.PostAsync<StringResponse>(Path, Body, CancellationToken.None);
		_ = requestInvoker.LastHttpClientHandler.UseProxy.Should().BeTrue();
	}

	[Fact]
	public async Task HttpClientUseTransferEncodingChunkedWhenTransferEncodingChunkedTrue()
	{
		var requestInvoker = new TrackingRequestInvoker(responseMessage =>
		{
			_ = responseMessage.RequestMessage.Content.Headers.ContentLength.Should().BeNull();
		});
		var transport = Setup(requestInvoker, transferEncodingChunked: true);

		_ = transport.Post<StringResponse>(Path, Body);
		_ = await transport.PostAsync<StringResponse>(Path, Body, CancellationToken.None);
	}

	[Fact]
	public async Task HttpClientSetsContentLengthWhenTransferEncodingChunkedFalse()
	{
		var trackingRequestInvoker = new TrackingRequestInvoker(responseMessage =>
		{
			_ = responseMessage.RequestMessage.Content.Headers.ContentLength.Should().HaveValue();
		});
		var transport = Setup(trackingRequestInvoker, transferEncodingChunked: false);

		_ = transport.Post<StringResponse>(Path, Body);
		_ = await transport.PostAsync<StringResponse>(Path, Body, CancellationToken.None);
	}

	[Fact]
	public async Task HttpClientSetsContentLengthWhenTransferEncodingChunkedHttpCompression()
	{
		var trackingRequestInvoker = new TrackingRequestInvoker(responseMessage =>
		{
			_ = responseMessage.RequestMessage.Content.Headers.ContentLength.Should().HaveValue();
		});
		var transport = Setup(trackingRequestInvoker, transferEncodingChunked: false, httpCompression: true);

		_ = transport.Post<StringResponse>(Path, Body);
		_ = await transport.PostAsync<StringResponse>(Path, Body, CancellationToken.None);
	}
}
