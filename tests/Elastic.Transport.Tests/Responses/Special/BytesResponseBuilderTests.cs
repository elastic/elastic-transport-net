// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Elastic.Transport.Tests.Shared;
using FluentAssertions;
using Xunit;

namespace Elastic.Transport.Tests.Responses.Special;

public class BytesResponseBuilderTests
{
	private static readonly byte[] Data = Encoding.UTF8.GetBytes("{\"_index\":\"my-index\",\"_id\":\"pZqC6JIB9RdSpcF8-3lq\",\"_version\":1,\"result\"" +
		":\"created\",\"_shards\":{\"total\":1,\"successful\":1,\"failed\":0},\"_seq_no\":2,\"_primary_term\":1}");

	[Fact]
	public async Task ReturnsExpectedResponse()
	{
		IResponseBuilder sut = new BytesResponseBuilder();

		var memoryStreamFactory = new TrackingMemoryStreamFactory();
		var config = new TransportConfiguration() { MemoryStreamFactory = memoryStreamFactory };
		var apiCallDetails = new ApiCallDetails();
		var boundConfiguration = new BoundConfiguration(config);
		var stream = new MemoryStream(Data);

		var result = await sut.BuildAsync<BytesResponse>(apiCallDetails, boundConfiguration, stream, BoundConfiguration.DefaultContentType, Data.Length);

		Validate(memoryStreamFactory, result);

		result = sut.Build<BytesResponse>(apiCallDetails, boundConfiguration, stream, BoundConfiguration.DefaultContentType, Data.Length);

		Validate(memoryStreamFactory, result);

		static void Validate(TrackingMemoryStreamFactory memoryStreamFactory, BytesResponse result)
		{
			result.Body.AsSpan().SequenceEqual(Data).Should().BeTrue();

			// As the incoming stream is seekable, no need to create a copy
			memoryStreamFactory.Created.Count.Should().Be(1);
		}
	}

	[Fact]
	public async Task ReturnsExpectedResponse_WhenDisableDirectStreaming()
	{
		IResponseBuilder sut = new BytesResponseBuilder();

		var memoryStreamFactory = new TrackingMemoryStreamFactory();
		var config = new TransportConfiguration() { DisableDirectStreaming = true, MemoryStreamFactory = memoryStreamFactory };
		var apiCallDetails = new ApiCallDetails() { ResponseBodyInBytes = Data };
		var boundConfiguration = new BoundConfiguration(config);
		var stream = new MemoryStream(Data);

		var result = await sut.BuildAsync<BytesResponse>(apiCallDetails, boundConfiguration, stream, BoundConfiguration.DefaultContentType, Data.Length);

		Validate(memoryStreamFactory, result);

		memoryStreamFactory.Reset();
		stream.Position = 0;

		result = sut.Build<BytesResponse>(apiCallDetails, boundConfiguration, stream, BoundConfiguration.DefaultContentType, Data.Length);

		Validate(memoryStreamFactory, result);

		static void Validate(TrackingMemoryStreamFactory memoryStreamFactory, BytesResponse result)
		{
			result.Body.AsSpan().SequenceEqual(Data).Should().BeTrue();

			memoryStreamFactory.Created.Count.Should().Be(0);
			foreach (var memoryStream in memoryStreamFactory.Created)
				memoryStream.IsDisposed.Should().BeTrue();
		}
	}
}
