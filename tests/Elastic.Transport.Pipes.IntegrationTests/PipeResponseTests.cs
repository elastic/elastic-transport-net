// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Buffers;
using System.IO.Pipelines;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Elastic.Transport.Pipes.IntegrationTests;

/// <summary>
/// Tests for <see cref="PipeResponse"/> which exposes the response body as a <see cref="PipeReader"/>.
/// </summary>
public class PipeResponseTests : IClassFixture<MockElasticsearchServer>
{
	private readonly MockElasticsearchServer _server;

	public PipeResponseTests(MockElasticsearchServer server) => _server = server;

	[Fact]
	public async Task PipeResponseDeserializesFromPipeReader()
	{
		// Act
		await using var response = await _server.Transport.RequestAsync<PipeResponse>(
			HttpMethod.GET,
			"/test-index/_doc/123");

		// Assert
		response.ApiCallDetails.HasSuccessfulStatusCode.Should().BeTrue();
		response.ContentType.Should().Be("application/json");

		// Deserialize directly from PipeReader (the .NET 10 feature)
		var document = await JsonSerializer.DeserializeAsync<JsonElement>(response.Body);

		document.GetProperty("_index").GetString().Should().Be("test-index");
		document.GetProperty("_id").GetString().Should().Be("123");
		document.GetProperty("found").GetBoolean().Should().BeTrue();
		document.GetProperty("_source").GetProperty("title").GetString().Should().Be("Document 123");
	}

	[Fact]
	public async Task PipeResponseWorksWithSearchEndpoint()
	{
		// Act
		await using var response = await _server.Transport.RequestAsync<PipeResponse>(
			HttpMethod.GET,
			"/my-index/_search?q=hello");

		// Assert
		response.ApiCallDetails.HasSuccessfulStatusCode.Should().BeTrue();

		var searchResult = await JsonSerializer.DeserializeAsync<JsonElement>(response.Body);

		searchResult.GetProperty("took").GetInt32().Should().Be(5);
		searchResult.GetProperty("timed_out").GetBoolean().Should().BeFalse();
		searchResult.GetProperty("hits").GetProperty("total").GetProperty("value").GetInt32().Should().Be(2);

		var hits = searchResult.GetProperty("hits").GetProperty("hits");
		hits.GetArrayLength().Should().Be(2);
	}

	[Fact]
	public async Task PipeResponseCopyToAsyncWritesToPipeWriter()
	{
		// Arrange
		var outputPipe = new Pipe();

		// Act
		await using var response = await _server.Transport.RequestAsync<PipeResponse>(
			HttpMethod.GET,
			"/test-index/_doc/456");

		// Copy to our pipe (simulating Response.BodyWriter)
		var copyTask = response.CopyToAsync(outputPipe.Writer);

		// Read from the pipe
		var readResult = await outputPipe.Reader.ReadAsync();
		var copiedBytes = readResult.Buffer.IsSingleSegment
			? readResult.Buffer.FirstSpan.ToArray()
			: readResult.Buffer.ToArray();
		outputPipe.Reader.AdvanceTo(readResult.Buffer.End);

		await copyTask;
		await outputPipe.Writer.CompleteAsync();
		await outputPipe.Reader.CompleteAsync();

		// Assert
		copiedBytes.Should().NotBeEmpty();
		var copiedJson = Encoding.UTF8.GetString(copiedBytes);
		var document = JsonSerializer.Deserialize<JsonElement>(copiedJson);
		document.GetProperty("_id").GetString().Should().Be("456");
	}

	[Fact]
	public async Task PipeResponseHandlesLargeResponse()
	{
		// Act
		await using var response = await _server.Transport.RequestAsync<PipeResponse>(
			HttpMethod.GET,
			"/large-response");

		// Assert
		response.ApiCallDetails.HasSuccessfulStatusCode.Should().BeTrue();

		var array = await JsonSerializer.DeserializeAsync<JsonElement[]>(response.Body);
		array.Should().HaveCount(100);
		array![0].GetProperty("id").GetInt32().Should().Be(0);
		array[99].GetProperty("id").GetInt32().Should().Be(99);
	}

	[Fact]
	public async Task PipeResponseHandlesChunkedResponse()
	{
		// Act
		await using var response = await _server.Transport.RequestAsync<PipeResponse>(
			HttpMethod.GET,
			"/chunked");

		// Assert
		response.ApiCallDetails.HasSuccessfulStatusCode.Should().BeTrue();

		var document = await JsonSerializer.DeserializeAsync<JsonElement>(response.Body);
		document.GetProperty("status").GetString().Should().Be("ok");
		document.GetProperty("message").GetString().Should().Be("Hello from chunked response");
	}

	[Fact]
	public async Task PipeResponseCopyToAsyncWorksWithMultipleReads()
	{
		// Arrange - Use a large response that requires multiple reads
		var outputPipe = new Pipe();
		var allBytes = new List<byte>();

		// Act
		await using var response = await _server.Transport.RequestAsync<PipeResponse>(
			HttpMethod.GET,
			"/large-response");

		// Start copying
		var copyTask = Task.Run(async () =>
		{
			await response.CopyToAsync(outputPipe.Writer);
			await outputPipe.Writer.CompleteAsync();
		});

		// Read all data
		while (true)
		{
			var readResult = await outputPipe.Reader.ReadAsync();
			foreach (var segment in readResult.Buffer)
				allBytes.AddRange(segment.ToArray());
			outputPipe.Reader.AdvanceTo(readResult.Buffer.End);

			if (readResult.IsCompleted)
				break;
		}

		await copyTask;
		await outputPipe.Reader.CompleteAsync();

		// Assert
		allBytes.Should().NotBeEmpty();
		var json = Encoding.UTF8.GetString(allBytes.ToArray());
		var array = JsonSerializer.Deserialize<JsonElement[]>(json);
		array.Should().HaveCount(100);
	}

	[Fact]
	public async Task PipeResponseDisposeCompletesReader()
	{
		// Act
		var response = await _server.Transport.RequestAsync<PipeResponse>(
			HttpMethod.GET,
			"/test-index/_doc/1");

		// Get reference to body before dispose
		var body = response.Body;

		// Dispose the response
		await response.DisposeAsync();

		// Assert - After dispose, reading should throw as the reader is completed
		await Assert.ThrowsAsync<InvalidOperationException>(async () => await body.ReadAsync());
	}

	[Fact]
	public async Task PipeResponseContentTypeIsSet()
	{
		// Act
		await using var response = await _server.Transport.RequestAsync<PipeResponse>(
			HttpMethod.GET,
			"/test-index/_doc/1");

		// Assert
		response.ContentType.Should().Be("application/json");
	}

	[Fact]
	public async Task PipeResponseWithPostRequest()
	{
		// Arrange
		var postData = PostData.Serializable(new { query = new { match_all = new { } } });

		// Act
		await using var response = await _server.Transport.RequestAsync<PipeResponse>(
			HttpMethod.POST,
			"/test-index/_search",
			postData);

		// Assert
		response.ApiCallDetails.HasSuccessfulStatusCode.Should().BeTrue();

		var searchResult = await JsonSerializer.DeserializeAsync<JsonElement>(response.Body);
		searchResult.GetProperty("hits").GetProperty("total").GetProperty("value").GetInt32().Should().Be(2);
	}
}
