// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Elastic.Transport.Tests.Plumbing;
using FluentAssertions;
using Xunit;

namespace Elastic.Transport.Tests;

public class ResponseBuilderDisposeTests
{
	private readonly ITransportConfiguration _settings = InMemoryConnectionFactory.Create().DisableDirectStreaming(false);
	private readonly ITransportConfiguration _settingsDisableDirectStream = InMemoryConnectionFactory.Create().DisableDirectStreaming();

	[Fact]
	public async Task StreamResponseWithPotentialBody_StreamIsNotDisposed() => await AssertResponse<StreamResponse>(false, expectedDisposed: false);

	[Fact]
	public async Task StreamResponseWithPotentialBodyAndDisableDirectStreaming_MemoryStreamIsNotDisposed() => await AssertResponse<StreamResponse>(true, expectedDisposed: false);

	[Fact]
	public async Task StreamResponseWith204StatusCode_StreamIsDisposed() => await AssertResponse<StreamResponse>(false, 204);

	[Fact]
	public async Task StreamResponseForHeadRequest_StreamIsDisposed() => await AssertResponse<StreamResponse>(false, httpMethod: HttpMethod.HEAD);

	[Fact]
	public async Task StreamResponseWithZeroContentLength_StreamIsDisposed() => await AssertResponse<StreamResponse>(false, contentLength: 0);

	[Fact]
	public async Task ResponseWithPotentialBody_StreamIsDisposed() => await AssertResponse<TestResponse>(false, expectedDisposed: true);

	[Fact]
	public async Task ResponseWithPotentialBodyAndDisableDirectStreaming_MemoryStreamIsDisposed() => await AssertResponse<TestResponse>(true, expectedDisposed: true);

	[Fact]
	public async Task ResponseWith204StatusCode_StreamIsDisposed() => await AssertResponse<TestResponse>(false, 204);

	[Fact]
	public async Task ResponseForHeadRequest_StreamIsDisposed() => await AssertResponse<TestResponse>(false, httpMethod: HttpMethod.HEAD);

	[Fact]
	public async Task ResponseWithZeroContentLength_StreamIsDisposed() => await AssertResponse<TestResponse>(false, contentLength: 0);

	[Fact]
	public async Task StringResponseWithPotentialBodyAndDisableDirectStreaming_MemoryStreamIsDisposed() => await AssertResponse<StringResponse>(true, expectedDisposed: true, memoryStreamCreateExpected: 1);

	private async Task AssertResponse<T>(bool disableDirectStreaming, int statusCode = 200, HttpMethod httpMethod = HttpMethod.GET, int contentLength = 10, bool expectedDisposed = true, int memoryStreamCreateExpected = -1)
		 where T : TransportResponse, new()
	{
		var settings = disableDirectStreaming ? _settingsDisableDirectStream : _settings;
		var memoryStreamFactory = new TrackMemoryStreamFactory();

		var requestData = new RequestData(httpMethod, "/", null, settings, null, null, memoryStreamFactory, default)
		{
			Node = new Node(new Uri("http://localhost:9200"))
		};

		var stream = new TrackDisposeStream();

		var response = _settings.ProductRegistration.ResponseBuilder.ToResponse<T>(requestData, null, statusCode, null, stream, null, contentLength, null, null);

		response.Should().NotBeNull();

		memoryStreamFactory.Created.Count.Should().Be(memoryStreamCreateExpected > -1 ? memoryStreamCreateExpected : disableDirectStreaming ? 1 : 0);
		if (disableDirectStreaming)
		{
			var memoryStream = memoryStreamFactory.Created[0];
			stream.IsDisposed.Should().BeTrue();
			memoryStream.IsDisposed.Should().Be(expectedDisposed);
		}
		else
		{
			stream.IsDisposed.Should().Be(expectedDisposed);
		}

		stream = new TrackDisposeStream();
		var ct = new CancellationToken();

		response = await _settings.ProductRegistration.ResponseBuilder.ToResponseAsync<T>(requestData, null, statusCode, null, stream, null, contentLength, null, null,
			cancellationToken: ct);

		response.Should().NotBeNull();

		memoryStreamFactory.Created.Count.Should().Be(memoryStreamCreateExpected > -1 ? memoryStreamCreateExpected + 1: disableDirectStreaming ? 2 : 0);
		if (disableDirectStreaming)
		{
			var memoryStream = memoryStreamFactory.Created[0];
			stream.IsDisposed.Should().BeTrue();
			memoryStream.IsDisposed.Should().Be(expectedDisposed);
		}
		else
		{
			stream.IsDisposed.Should().Be(expectedDisposed);
		}
	}

	private class TrackDisposeStream : MemoryStream
	{
		public TrackDisposeStream() { }

		public TrackDisposeStream(byte[] bytes) : base(bytes) { }

		public TrackDisposeStream(byte[] bytes, int index, int count) : base(bytes, index, count) { }

		public bool IsDisposed { get; private set; }

		protected override void Dispose(bool disposing)
		{
			IsDisposed = true;
			base.Dispose(disposing);
		}
	}

	private class TrackMemoryStreamFactory : MemoryStreamFactory
	{
		public IList<TrackDisposeStream> Created { get; } = [];

		public override MemoryStream Create()
		{
			var stream = new TrackDisposeStream();
			Created.Add(stream);
			return stream;
		}

		public override MemoryStream Create(byte[] bytes)
		{
			var stream = new TrackDisposeStream(bytes);
			Created.Add(stream);
			return stream;
		}

		public override MemoryStream Create(byte[] bytes, int index, int count)
		{
			var stream = new TrackDisposeStream(bytes, index, count);
			Created.Add(stream);
			return stream;
		}
	}
}
