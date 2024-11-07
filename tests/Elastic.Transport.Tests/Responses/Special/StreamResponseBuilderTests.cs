// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Buffers;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Elastic.Transport.Tests.Shared;
using FluentAssertions;
using Xunit;

namespace Elastic.Transport.Tests.Responses.Special;

public class StreamResponseBuilderTests
{
	private static readonly byte[] Data = Encoding.UTF8.GetBytes("{\"_index\":\"my-index\",\"_id\":\"pZqC6JIB9RdSpcF8-3lq\",\"_version\":1,\"result\"" +
		":\"created\",\"_shards\":{\"total\":1,\"successful\":1,\"failed\":0},\"_seq_no\":2,\"_primary_term\":1}");

	[Fact]
	public async Task ReturnsExpectedResponse()
	{
		IResponseBuilder sut = new StreamResponseBuilder();

		var memoryStreamFactory = new TrackingMemoryStreamFactory();
		var config = new TransportConfiguration() { MemoryStreamFactory = memoryStreamFactory };
		var apiCallDetails = new ApiCallDetails();
		var requestData = new RequestData(config);
		var stream = new MemoryStream(Data);

		var result = await sut.BuildAsync<StreamResponse>(apiCallDetails, requestData, stream, RequestData.DefaultContentType, Data.Length);
		await ValidateAsync(memoryStreamFactory, result);

		memoryStreamFactory.Reset();
		stream.Position = 0;

		result = sut.Build<StreamResponse>(apiCallDetails, requestData, stream, RequestData.DefaultContentType, Data.Length);
		await ValidateAsync(memoryStreamFactory, result);
	}

	[Fact]
	public async Task ReturnsExpectedResponse_WhenDisableDirectStreaming()
	{
		IResponseBuilder sut = new StreamResponseBuilder();

		var memoryStreamFactory = new TrackingMemoryStreamFactory();
		var config = new TransportConfiguration() { MemoryStreamFactory = memoryStreamFactory, DisableDirectStreaming = true };
		var apiCallDetails = new ApiCallDetails() { ResponseBodyInBytes = Data };
		var requestData = new RequestData(config);
		var stream = new MemoryStream(Data);

		var result = await sut.BuildAsync<StreamResponse>(apiCallDetails, requestData, stream, RequestData.DefaultContentType, Data.Length);
		await ValidateAsync(memoryStreamFactory, result);

		memoryStreamFactory.Reset();
		stream.Position = 0;

		result = sut.Build<StreamResponse>(apiCallDetails, requestData, stream, RequestData.DefaultContentType, Data.Length);
		await ValidateAsync(memoryStreamFactory, result);
	}

	private static async Task ValidateAsync(TrackingMemoryStreamFactory memoryStreamFactory, StreamResponse result)
	{
		var buffer = ArrayPool<byte>.Shared.Rent(Data.Length);

#pragma warning disable CA1835 // Prefer the 'Memory'-based overloads for 'ReadAsync' and 'WriteAsync'
		var read = await result.Body.ReadAsync(buffer, 0, Data.Length);
#pragma warning restore CA1835 // Prefer the 'Memory'-based overloads for 'ReadAsync' and 'WriteAsync'

#pragma warning disable IDE0057 // Use range operator
		buffer.AsSpan().Slice(0, read).SequenceEqual(Data).Should().BeTrue();
#pragma warning restore IDE0057 // Use range operator

		memoryStreamFactory.Created.Count.Should().Be(0);
	}
}
