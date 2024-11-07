// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Text;
using System.Threading.Tasks;
using Elastic.Transport.Products;
using Elastic.Transport.Products.Elasticsearch;
using Elastic.Transport.Tests.Plumbing;
using Elastic.Transport.Tests.Shared;
using FluentAssertions;
using Xunit;

namespace Elastic.Transport.Tests;

public class ResponseFactoryDisposeTests
{
	[Fact]
	public async Task StreamResponse_WithPotentialBody_StreamIsNotDisposed() =>
		// We expect no streams to be created as the original response stream should be directly returned and not disposed
		await AssertResponse<StreamResponse>(disableDirectStreaming: false, expectMemoryStreamDisposal: false);

	[Fact]
	public async Task StreamResponse_WithPotentialBody_AndDisableDirectStreaming_MemoryStreamIsNotDisposed() =>
		await AssertResponse<StreamResponse>(disableDirectStreaming: true, expectMemoryStreamDisposal: false, memoryStreamCreateExpected: 1);

	[Fact]
	public async Task Response_WithPotentialBody_AndDisableDirectStreaming_ButInvalidContentType_MemoryStreamIsDisposed() =>
		await AssertResponse<TestResponse>(disableDirectStreaming: true, contentType: "application/not-valid", expectMemoryStreamDisposal: true,
			memoryStreamCreateExpected: 1);

	[Fact]
	public async Task Response_WithPotentialBody_AndDisableDirectStreaming_ButSkippedStatusCode_MemoryStreamIsDisposed() =>
		await AssertResponse<TestResponse>(disableDirectStreaming: true, skipStatusCode: 200, expectMemoryStreamDisposal: true, memoryStreamCreateExpected: 1);

	[Fact]
	public async Task Response_WithPotentialBody_AndDisableDirectStreaming_ButEmptyJson_MemoryStreamIsDisposed() =>
		await AssertResponse<TestResponse>(disableDirectStreaming: true, responseJson: "  ", expectMemoryStreamDisposal: true, memoryStreamCreateExpected: 1);

	[Fact]
	public async Task Response_WithPotentialBody_AndNotDisableDirectStreaming_ButEmptyJson_MemoryStreamIsDisposed() =>
		await AssertResponse<TestResponse>(disableDirectStreaming: false, responseJson: "  ", expectMemoryStreamDisposal: true, memoryStreamCreateExpected: 0);

	[Fact]
	// NOTE: The empty string here hits a fast path in STJ which returns default if the stream length is zero.
	public async Task Response_WithPotentialBody_AndDisableDirectStreaming_ButNullResponseDuringDeserialization_MemoryStreamIsDisposed() =>
		await AssertResponse<TestResponse>(disableDirectStreaming: true, responseJson: "", expectMemoryStreamDisposal: true, memoryStreamCreateExpected: 1);

	[Fact]
	// NOTE: The empty string here hits a fast path in STJ which returns default if the stream length is zero.
	public async Task Response_WithPotentialBody_AndNotDisableDirectStreaming_ButNullResponseDuringDeserialization_MemoryStreamIsDisposed() =>
		await AssertResponse<TestResponse>(disableDirectStreaming: false, responseJson: "", expectMemoryStreamDisposal: true, memoryStreamCreateExpected: 0);

	[Fact]
	// NOTE: We expect one memory stream factory creation when handling error responses even when not using DisableDirectStreaming
	public async Task Response_WithPotentialBody_AndNotDisableDirectStreaming_AndErrorResponse_StreamIsDisposed() =>
		await AssertResponse<TestResponse>(disableDirectStreaming: false, productRegistration: new ElasticsearchProductRegistration(), statusCode: 400,
			expectMemoryStreamDisposal: true, memoryStreamCreateExpected: 1);

	[Fact]
	public async Task Response_WithPotentialBody_AndDisableDirectStreaming_AndErrorResponse_StreamIsDisposed() =>
		await AssertResponse<TestResponse>(disableDirectStreaming: true, productRegistration: new ElasticsearchProductRegistration(), statusCode: 400,
			expectMemoryStreamDisposal: true, memoryStreamCreateExpected: 1);

	[Fact]
	public async Task StringResponse_WithPotentialBody_AndNotDisableDirectStreaming_AndNotChunkedReponse_NoMemoryStreamIsCreated() =>
		await AssertResponse<StringResponse>(disableDirectStreaming: false, expectMemoryStreamDisposal: true, memoryStreamCreateExpected: 0);

	[Fact]
	public async Task StringResponse_WithPotentialBody_AndNotDisableDirectStreaming_AndChunkedReponse_NoMemoryStreamIsCreated() =>
		await AssertResponse<StringResponse>(disableDirectStreaming: false, expectMemoryStreamDisposal: true, memoryStreamCreateExpected: 0, isChunked: true);

	[Fact]
	public async Task StringResponse_WithPotentialBody_AndDisableDirectStreaming_AndChunkedReponse_MemoryStreamIsDisposed() =>
		await AssertResponse<StringResponse>(disableDirectStreaming: true, expectMemoryStreamDisposal: true, memoryStreamCreateExpected: 1, isChunked: true);

	[Fact]
	public async Task StringResponse_WithPotentialBody_AndDisableDirectStreaming_AndNotChunkedReponse_MemoryStreamIsDisposed() =>
		await AssertResponse<StringResponse>(disableDirectStreaming: true, expectMemoryStreamDisposal: true, memoryStreamCreateExpected: 1);

	private async Task AssertResponse<T>(bool disableDirectStreaming, int statusCode = 200, HttpMethod httpMethod = HttpMethod.GET, bool isChunked = true,
		bool expectMemoryStreamDisposal = true, string contentType = "application/json", string responseJson = "{}", int skipStatusCode = -1,
		ProductRegistration productRegistration = null, int memoryStreamCreateExpected = 0, bool responseStreamCanSeek = false)
			where T : TransportResponse, new()
	{
		ITransportConfiguration config;

		var memoryStreamFactory = new TrackingMemoryStreamFactory();

		if (skipStatusCode > -1)
		{
			config = InMemoryConnectionFactory.Create(productRegistration) with
			{
				DisableDirectStreaming = disableDirectStreaming,
				SkipDeserializationForStatusCodes = [skipStatusCode],
				MemoryStreamFactory = memoryStreamFactory
			};
		}
		else
		{
			config = InMemoryConnectionFactory.Create(productRegistration) with
			{
				DisableDirectStreaming = disableDirectStreaming,
				MemoryStreamFactory = memoryStreamFactory
			};
		}

		var endpoint = new Endpoint(new EndpointPath(httpMethod, "/"), new Node(new Uri("http://localhost:9200")));

		var requestData = new RequestData(config, null);

		var stream = new TrackDisposeStream(responseStreamCanSeek);

		if (!string.IsNullOrEmpty(responseJson))
		{
			stream.Write(Encoding.UTF8.GetBytes(responseJson), 0, responseJson.Length);
			stream.Position = 0;
		}

		var response = config.RequestInvoker.ResponseFactory.Create<T>(endpoint, requestData, null, null, statusCode, null, stream, contentType, isChunked ? -1 : responseJson.Length, null, null);

		Validate(disableDirectStreaming, expectMemoryStreamDisposal, memoryStreamCreateExpected, memoryStreamFactory, stream, response);

		memoryStreamFactory.Reset();
		stream = new TrackDisposeStream(responseStreamCanSeek);
		if (!string.IsNullOrEmpty(responseJson))
		{
			stream.Write(Encoding.UTF8.GetBytes(responseJson), 0, responseJson.Length);
			stream.Position = 0;
		}

		response = await config.RequestInvoker.ResponseFactory.CreateAsync<T>(endpoint, requestData, null, null, statusCode, null, stream, contentType, isChunked ? -1 : responseJson.Length, null, null);

		Validate(disableDirectStreaming, expectMemoryStreamDisposal, memoryStreamCreateExpected, memoryStreamFactory, stream, response);

		static void Validate(bool disableDirectStreaming, bool expectedDisposed, int memoryStreamCreateExpected, TrackingMemoryStreamFactory memoryStreamFactory, TrackDisposeStream stream, T response) 
		{
			response.Should().NotBeNull();

			// The latest implementation should never dispose the incoming stream and assumes the caller will handler disposal
			stream.IsDisposed.Should().Be(false);

			memoryStreamFactory.Created.Count.Should().Be(memoryStreamCreateExpected);

			if (disableDirectStreaming)
				memoryStreamFactory.Created[0].IsDisposed.Should().Be(expectedDisposed);
		}
	}
}
