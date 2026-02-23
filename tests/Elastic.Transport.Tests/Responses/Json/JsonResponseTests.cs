// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.IO;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace Elastic.Transport.Tests.Responses.Json;

public class JsonResponseTests
{
	// --- Builder tests ---

	[Fact]
	public async Task BuilderJsonContent()
	{
		IResponseBuilder sut = new JsonResponseBuilder();
		var config = new TransportConfiguration();
		var apiCallDetails = new ApiCallDetails();
		var boundConfiguration = new BoundConfiguration(config);

		var data = Encoding.UTF8.GetBytes("""{"_index":"my-index","_id":"1","found":true}""");
		var stream = new MemoryStream(data);

		var result = await sut.BuildAsync<JsonResponse>(apiCallDetails, boundConfiguration, stream, BoundConfiguration.DefaultContentType, data.Length);
		result.Get<string>("_index").Should().Be("my-index");
		result.Get<bool>("found").Should().BeTrue();

		stream.Position = 0;

		result = sut.Build<JsonResponse>(apiCallDetails, boundConfiguration, stream, BoundConfiguration.DefaultContentType, data.Length);
		result.Get<string>("_index").Should().Be("my-index");
	}

	[Fact]
	public async Task BuilderNonJsonContent()
	{
		IResponseBuilder sut = new JsonResponseBuilder();
		var config = new TransportConfiguration();
		var apiCallDetails = new ApiCallDetails();
		var boundConfiguration = new BoundConfiguration(config);

		var data = Encoding.UTF8.GetBytes("This is not JSON");
		var stream = new MemoryStream(data);

		var result = await sut.BuildAsync<JsonResponse>(apiCallDetails, boundConfiguration, stream, "text/plain", data.Length);
		result.Get<string>("body").Should().Be("This is not JSON");

		stream.Position = 0;

		result = sut.Build<JsonResponse>(apiCallDetails, boundConfiguration, stream, "text/plain", data.Length);
		result.Get<string>("body").Should().Be("This is not JSON");
	}

	// --- Direct DOM access ---

	[Fact]
	public void DirectDomAccess()
	{
		var node = JsonNode.Parse("""{"hits":{"total":42}}""");
		var response = new JsonResponse(node);

		response.Body["hits"]["total"].GetValue<int>().Should().Be(42);
	}

	[Fact]
	public void DirectDomAccessArray()
	{
		var node = JsonNode.Parse("""{"items":["a","b","c"]}""");
		var response = new JsonResponse(node);

		response.Body["items"][0].GetValue<string>().Should().Be("a");
		response.Body["items"][2].GetValue<string>().Should().Be("c");
	}

	// --- Get<T> path traversal ---

	[Fact]
	public void FlatString()
	{
		var response = new JsonResponse(JsonNode.Parse("""{"name":"hello"}"""));
		response.Get<string>("name").Should().Be("hello");
	}

	[Fact]
	public void FlatInt()
	{
		var response = new JsonResponse(JsonNode.Parse("""{"count":42}"""));
		response.Get<int>("count").Should().Be(42);
	}

	[Fact]
	public void FlatLong()
	{
		var response = new JsonResponse(JsonNode.Parse("""{"big":9999999999}"""));
		response.Get<long>("big").Should().Be(9999999999L);
	}

	[Fact]
	public void FlatDouble()
	{
		var response = new JsonResponse(JsonNode.Parse("""{"pi":3.14}"""));
		response.Get<double>("pi").Should().BeApproximately(3.14, 0.001);
	}

	[Fact]
	public void FlatBool()
	{
		var response = new JsonResponse(JsonNode.Parse("""{"flag":true}"""));
		response.Get<bool>("flag").Should().BeTrue();
	}

	[Fact]
	public void NestedPath()
	{
		var response = new JsonResponse(JsonNode.Parse("""{"a":{"b":{"c":"deep"}}}"""));
		response.Get<string>("a.b.c").Should().Be("deep");
	}

	[Fact]
	public void ArrayByNumericIndex()
	{
		var response = new JsonResponse(JsonNode.Parse("""{"items":[10,20,30]}"""));
		response.Get<int>("items.0").Should().Be(10);
		response.Get<int>("items.2").Should().Be(30);
	}

	[Fact]
	public void ArrayOfObjectsByIndex()
	{
		var response = new JsonResponse(JsonNode.Parse("""{"hits":[{"name":"a"},{"name":"b"}]}"""));
		response.Get<string>("hits.0.name").Should().Be("a");
		response.Get<string>("hits.1.name").Should().Be("b");
	}

	[Fact]
	public void BracketIndex()
	{
		var response = new JsonResponse(JsonNode.Parse("""{"items":[10,20,30]}"""));
		response.Get<int>("items.[0]").Should().Be(10);
		response.Get<int>("items.[2]").Should().Be(30);
	}

	[Fact]
	public void BracketFirst()
	{
		var response = new JsonResponse(JsonNode.Parse("""{"items":["a","b","c"]}"""));
		response.Get<string>("items.[first()]").Should().Be("a");
	}

	[Fact]
	public void UnderscoreFirst()
	{
		var response = new JsonResponse(JsonNode.Parse("""{"items":["a","b","c"]}"""));
		response.Get<string>("items._first_").Should().Be("a");
	}

	[Fact]
	public void BracketLast()
	{
		var response = new JsonResponse(JsonNode.Parse("""{"items":["a","b","c"]}"""));
		response.Get<string>("items.[last()]").Should().Be("c");
	}

	[Fact]
	public void UnderscoreLast()
	{
		var response = new JsonResponse(JsonNode.Parse("""{"items":["a","b","c"]}"""));
		response.Get<string>("items._last_").Should().Be("c");
	}

	[Fact]
	public void DeepNestedWithArrays()
	{
		var response = new JsonResponse(JsonNode.Parse("""{"hits":{"hits":[{"_source":{"name":"first"}},{"_source":{"name":"second"}}]}}"""));
		response.Get<string>("hits.hits.[0]._source.name").Should().Be("first");
		response.Get<string>("hits.hits.[last()]._source.name").Should().Be("second");
	}

	[Fact]
	public void ArbitraryKeyTraversesIntoFirstKey()
	{
		var response = new JsonResponse(JsonNode.Parse("""{"data":{"some_key":{"value":1}}}"""));
		response.Get<int>("data._arbitrary_key_.value").Should().Be(1);
	}

	[Fact]
	public void ArbitraryKeyReturnsKeyName()
	{
		var response = new JsonResponse(JsonNode.Parse("""{"data":{"first_key":"v1","second_key":"v2"}}"""));
		response.Get<string>("data._arbitrary_key_").Should().NotBeNull();
	}

	[Fact]
	public void MissingPath() =>
		new JsonResponse(JsonNode.Parse("""{"a":1}""")).Get<string>("b").Should().BeNull();

	[Fact]
	public void NullPath() =>
		new JsonResponse(JsonNode.Parse("""{"a":1}""")).Get<string>(null).Should().BeNull();

	[Fact]
	public void NullBody() =>
		new JsonResponse(null).Get<string>("a").Should().BeNull();

	[Fact]
	public void OutOfBoundsIndex() =>
		new JsonResponse(JsonNode.Parse("""{"items":[1]}""")).Get<int>("items.99").Should().Be(0);

	[Fact]
	public void NumberAsString() =>
		new JsonResponse(JsonNode.Parse("""{"v":42}""")).Get<string>("v").Should().Be("42");

	[Fact]
	public void DateTimeParsing()
	{
		var response = new JsonResponse(JsonNode.Parse("""{"d":"2024-01-15T10:30:00Z"}"""));
		response.Get<DateTime>("d").Should().Be(new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc));
	}
}
