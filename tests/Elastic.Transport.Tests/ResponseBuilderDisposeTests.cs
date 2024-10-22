// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
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

	[Fact]
	public async Task ResponseWithPotentialBody_StreamIsNotDisposed() => await AssertResponse(expectedDisposed: false);

	[Fact]
	public async Task ResponseWith204StatusCode_StreamIsDisposed() => await AssertResponse(204);

	[Fact]
	public async Task ResponseForHeadRequest_StreamIsDisposed() => await AssertResponse(httpMethod: HttpMethod.HEAD);

	[Fact]
	public async Task ResponseWithZeroContentLength_StreamIsDisposed() => await AssertResponse(contentLength: 0);

	private async Task AssertResponse(int statusCode = 200, HttpMethod httpMethod = HttpMethod.GET, int contentLength = 10, bool expectedDisposed = true)
	{
		var settings = _settings;
		var requestData = new RequestData(httpMethod, "/", null, settings, null, null, null, default)
		{
			Node = new Node(new Uri("http://localhost:9200"))
		};

		var stream = new TrackDisposeStream();

		var response = _settings.ProductRegistration.ResponseBuilder.ToResponse<TestResponse>(requestData, null, statusCode, null, stream, null, contentLength, null, null);

		response.Should().NotBeNull();
		stream.IsDisposed.Should().Be(expectedDisposed);

		stream = new TrackDisposeStream();
		var ct = new CancellationToken();

		response = await _settings.ProductRegistration.ResponseBuilder.ToResponseAsync<TestResponse>(requestData, null, statusCode, null, stream, null, contentLength, null, null,
			cancellationToken: ct);

		response.Should().NotBeNull();
		stream.IsDisposed.Should().Be(expectedDisposed);
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
}
