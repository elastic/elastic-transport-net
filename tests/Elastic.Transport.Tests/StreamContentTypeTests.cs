// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Elastic.Transport.Products.Elasticsearch;
using FluentAssertions;
using Xunit;

namespace Elastic.Transport.Tests;

/// <summary>
/// A stream response whose server answered with another content type than the request accepted used to hand back an empty
/// <see cref="Stream.Null"/> body on a 200. It now fails loudly, and a caller can opt out with <c>Accept: */*</c>.
/// </summary>
public class StreamContentTypeTests
{
	private static readonly byte[] Body = Encoding.UTF8.GetBytes("data: hello\n\n");

	private static DistributedTransport TransportReturning(string contentType, int statusCode = 200) =>
		new(new TransportConfiguration(new SingleNodePool(new Uri("http://localhost:9200")),
			new InMemoryRequestInvoker(Body, statusCode, contentType: contentType)));

	private static readonly EndpointPath Path = new(HttpMethod.POST, "/converse/async");

	[Fact]
	public async Task AMismatchOnASuccessfulStreamResponseThrowsAndNamesBothContentTypes()
	{
		var transport = TransportReturning("application/octet-stream");
		var accept = new RequestConfiguration { Accept = "text/event-stream" };

		var thrown = await Assert.ThrowsAnyAsync<TransportException>(() => transport.RequestAsync<StreamResponse>(Path, null, null, accept));

		var message = thrown.ToString();
		_ = message.Should().Contain("text/event-stream").And.Contain("application/octet-stream");
	}

	[Fact]
	public void TheSyncPathThrowsToo()
	{
		var transport = TransportReturning("application/octet-stream");
		var accept = new RequestConfiguration { Accept = "text/event-stream" };

		_ = FluentActions.Invoking(() => transport.Request<StreamResponse>(Path, null, null, accept)).Should().Throw<TransportException>();
	}

#if NET10_0_OR_GREATER && !TRANSPORT_LEGACY_ASSET
	[Fact]
	public async Task APipeResponseThrowsToo()
	{
		var transport = TransportReturning("application/octet-stream");
		var accept = new RequestConfiguration { Accept = "text/event-stream" };

		_ = await Assert.ThrowsAnyAsync<TransportException>(() => transport.RequestAsync<PipeResponse>(Path, null, null, accept));
	}
#endif

	[Fact]
	public async Task AWildcardAcceptSkipsTheCheckAndGivesTheBody()
	{
		var transport = TransportReturning("application/octet-stream");
		var accept = new RequestConfiguration { Accept = "*/*" };

		using var response = await transport.RequestAsync<StreamResponse>(Path, null, null, accept);

		using var reader = new StreamReader(response.Body);
		_ = (await reader.ReadToEndAsync()).Should().Be("data: hello\n\n");
	}

	[Fact]
	public async Task AResponseWithoutAContentTypeIsLeftAsItWas()
	{
		// no Content-Type at all is not "a different type": it keeps returning the details with an empty body, as before
		var transport = TransportReturning("");

		using var response = await transport.RequestAsync<StreamResponse>(Path, null, null, null);

		_ = response.ApiCallDetails.HasExpectedContentType.Should().BeFalse();
		_ = response.Body.Should().BeSameAs(Stream.Null);
	}

	[Fact]
	public async Task AMatchingContentTypeStillWorks()
	{
		var transport = TransportReturning("text/event-stream; charset=utf-8");
		var accept = new RequestConfiguration { Accept = "text/event-stream" };

		using var response = await transport.RequestAsync<StreamResponse>(Path, null, null, accept);

		using var reader = new StreamReader(response.Body);
		_ = (await reader.ReadToEndAsync()).Should().Be("data: hello\n\n");
	}

	[Fact]
	public async Task AnErrorStatusStillReturnsItsDetailsInsteadOfThrowing()
	{
		// a 502 page from a proxy is html: the caller reads the status and the body from the details as before
		var transport = TransportReturning("text/html", 502);
		var accept = new RequestConfiguration { Accept = "text/event-stream", MaxRetries = 0 };

		using var response = await transport.RequestAsync<StreamResponse>(Path, null, null, accept);

		_ = response.ApiCallDetails.HttpStatusCode.Should().Be(502);
		_ = response.ApiCallDetails.ResponseBodyInBytes.Should().NotBeNullOrEmpty();
	}

	[Fact]
	public async Task AJsonResponseWithAMismatchKeepsItsBehaviour()
	{
		// this change is about responses that hand over the raw stream; typed responses keep reporting through ApiCallDetails
		var transport = TransportReturning("text/html");

		var response = await transport.RequestAsync<StringResponse>(Path, null, null, null);

		_ = response.ApiCallDetails.HasExpectedContentType.Should().BeFalse();
	}

	[Fact]
	public void TheElasticsearchProductAcceptsTheWildcardToo() =>
		_ = new ElasticsearchProductRegistration().IsExpectedResponseContentType("*/*", "application/octet-stream").Should().BeTrue();
}
