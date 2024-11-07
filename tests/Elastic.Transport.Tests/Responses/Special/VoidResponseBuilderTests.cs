// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.IO;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using static Elastic.Transport.VoidResponse;

namespace Elastic.Transport.Tests.Responses.Special;

public class VoidResponseBuilderTests
{
	private static readonly byte[] Data = Encoding.UTF8.GetBytes("{\"_index\":\"my-index\",\"_id\":\"pZqC6JIB9RdSpcF8-3lq\",\"_version\":1,\"result\"" +
		":\"created\",\"_shards\":{\"total\":1,\"successful\":1,\"failed\":0},\"_seq_no\":2,\"_primary_term\":1}");

	[Fact]
	public async Task ReturnsExpectedResponse()
	{
		IResponseBuilder sut = new VoidResponseBuilder();

		var config = new TransportConfiguration();
		var apiCallDetails = new ApiCallDetails();
		var requestData = new RequestData(config);
		var stream = new MemoryStream(Data);

		var result = await sut.BuildAsync<VoidResponse>(apiCallDetails, requestData, stream, RequestData.DefaultContentType, Data.Length);
		result.Body.Should().BeOfType(typeof(VoidBody));

		result = sut.Build<VoidResponse>(apiCallDetails, requestData, stream, RequestData.DefaultContentType, Data.Length);
		result.Body.Should().BeOfType(typeof(VoidBody));
	}

	[Fact]
	public async Task ReturnsExpectedResponse_WhenDisableDirectStreaming()
	{
		IResponseBuilder sut = new VoidResponseBuilder();

		var config = new TransportConfiguration() { DisableDirectStreaming = true };
		var apiCallDetails = new ApiCallDetails() { ResponseBodyInBytes = Data };
		var requestData = new RequestData(config);
		var stream = new MemoryStream(Data);

		var result = await sut.BuildAsync<VoidResponse>(apiCallDetails, requestData, stream, RequestData.DefaultContentType, Data.Length);
		result.Body.Should().BeOfType(typeof(VoidBody));

		result = sut.Build<VoidResponse>(apiCallDetails, requestData, stream, RequestData.DefaultContentType, Data.Length);
		result.Body.Should().BeOfType(typeof(VoidBody));
	}
}
