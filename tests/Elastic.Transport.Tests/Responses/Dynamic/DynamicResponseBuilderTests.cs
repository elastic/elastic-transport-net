// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.IO;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace Elastic.Transport.Tests.Responses.Dynamic;

public class DynamicResponseBuilderTests
{
	[Fact]
	public async Task ReturnsExpectedResponse_ForJsonData()
	{
		IResponseBuilder sut = new DynamicResponseBuilder();

		var config = new TransportConfiguration();
		var apiCallDetails = new ApiCallDetails();
		var requestData = new RequestData(config);

		var data = Encoding.UTF8.GetBytes("{\"_index\":\"my-index\",\"_id\":\"pZqC6JIB9RdSpcF8-3lq\",\"_version\":1,\"result\":\"created\",\"_shards\":{\"total\":1,\"successful\":1,\"failed\":0},\"_seq_no\":2,\"_primary_term\":1}");
		var stream = new MemoryStream(data);

		var result = await sut.BuildAsync<DynamicResponse>(apiCallDetails, requestData, stream, RequestData.DefaultContentType, data.Length);
		result.Body.Get<string>("_index").Should().Be("my-index");

		stream.Position = 0;

		result = sut.Build<DynamicResponse>(apiCallDetails, requestData, stream, RequestData.DefaultContentType, data.Length);
		result.Body.Get<string>("_index").Should().Be("my-index");
	}

	[Fact]
	public async Task ReturnsExpectedResponse_ForNonJsonData()
	{
		IResponseBuilder sut = new DynamicResponseBuilder();

		var config = new TransportConfiguration();
		var apiCallDetails = new ApiCallDetails();
		var requestData = new RequestData(config);

		var data = Encoding.UTF8.GetBytes("This is not JSON");
		var stream = new MemoryStream(data);

		var result = await sut.BuildAsync<DynamicResponse>(apiCallDetails, requestData, stream, "text/plain", data.Length);
		result.Body.Get<string>("body").Should().Be("This is not JSON");

		stream.Position = 0;

		result = sut.Build<DynamicResponse>(apiCallDetails, requestData, stream, "text/plain", data.Length);
		result.Body.Get<string>("body").Should().Be("This is not JSON");
	}
}
