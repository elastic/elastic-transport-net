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

namespace Elastic.Transport.IntegrationTests.Http
{
	[ApiController, Route("[controller]")]
	public class ChunkedController : ControllerBase
	{
		[HttpPost]
		public Task<JsonElement> Post([FromBody]JsonElement body) => Task.FromResult(body);
	}

	public class TransferEncodingChunkedTests : AssemblyServerTestsBase
	{
		public TransferEncodingChunkedTests(TransportTestServer instance) : base(instance) { }


		private static string BodyString = "{\"query\":{\"match_all\":{}}}";
		private static PostData Body = PostData.String(BodyString);
		private static readonly string Path = "/chunked";

		private Transport Setup(
			TestableHttpConnection connection,
			Uri proxyAddress = null,
			bool? disableAutomaticProxyDetection = null,
			bool httpCompression = false,
			bool transferEncodingChunked = false
		)
		{
			var connectionPool = new SingleNodeConnectionPool(Server.Uri);
			var config = new TransportConfiguration(connectionPool, connection)
				.TransferEncodingChunked(transferEncodingChunked)
				.EnableHttpCompression(httpCompression);
			config = disableAutomaticProxyDetection.HasValue
				? config.DisableAutomaticProxyDetection(disableAutomaticProxyDetection.Value)
				//make sure we the requests in debugging proxy
				: TransportTestServer.RerouteToProxyIfNeeded(config);

			return new Transport(config);
		}

		/// <summary>
		/// Setting HttpClientHandler.Proxy = null don't disable HttpClient automatic proxy detection.
		/// It is disabled by setting Proxy to non-null value or by setting UseProxy = false.
		/// </summary>
		[Fact] public async Task HttpClientUseProxyShouldBeFalseWhenDisabledAutoProxyDetection()
		{
			var connection = new TestableHttpConnection();
			var transport = Setup(connection, disableAutomaticProxyDetection: true);

			var r = transport.Post<StringResponse>(Path, Body);
			connection.LastHttpClientHandler.UseProxy.Should().BeFalse();
			r.Body.Should().Be(BodyString);

			r = await transport.PostAsync<StringResponse>(Path, Body, CancellationToken.None).ConfigureAwait(false);
			connection.LastHttpClientHandler.UseProxy.Should().BeFalse();
			r.Body.Should().Be(BodyString);
		}

		[Fact] public async Task HttpClientUseProxyShouldBeTrueWhenEnabledAutoProxyDetection()
		{
			var connection = new TestableHttpConnection();
			var transport = Setup(connection);

			transport.Post<StringResponse>(Path, Body);
			connection.LastHttpClientHandler.UseProxy.Should().BeTrue();
			await transport.PostAsync<StringResponse>(Path, Body, CancellationToken.None).ConfigureAwait(false);
			connection.LastHttpClientHandler.UseProxy.Should().BeTrue();
		}

		[Fact] public async Task HttpClientUseTransferEncodingChunkedWhenTransferEncodingChunkedTrue()
		{
			var connection = new TestableHttpConnection(responseMessage =>
			{
				responseMessage.RequestMessage.Content.Headers.ContentLength.Should().BeNull();
			});
			var transport = Setup(connection, transferEncodingChunked: true);

			transport.Post<StringResponse>(Path, Body);
			await transport.PostAsync<StringResponse>(Path, Body, CancellationToken.None).ConfigureAwait(false);
		}

		[Fact] public async Task HttpClientSetsContentLengthWhenTransferEncodingChunkedFalse()
		{
			var connection = new TestableHttpConnection(responseMessage =>
			{
				responseMessage.RequestMessage.Content.Headers.ContentLength.Should().HaveValue();
			});
			var transport = Setup(connection, transferEncodingChunked: false);

			transport.Post<StringResponse>(Path, Body);
			await transport.PostAsync<StringResponse>(Path, Body, CancellationToken.None).ConfigureAwait(false);
		}

		[Fact] public async Task HttpClientSetsContentLengthWhenTransferEncodingChunkedHttpCompression()
		{
			var connection = new TestableHttpConnection(responseMessage =>
			{
				responseMessage.RequestMessage.Content.Headers.ContentLength.Should().HaveValue();
			});
			var transport = Setup(connection, transferEncodingChunked: false, httpCompression: true);

			transport.Post<StringResponse>(Path, Body);
			await transport.PostAsync<StringResponse>(Path, Body, CancellationToken.None).ConfigureAwait(false);
		}
	}
}
