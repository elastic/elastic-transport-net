// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.IO.Pipelines;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Elastic.Transport.Pipes.IntegrationTests;

/// <summary>
/// Tests for <see cref="PostData.PipeReader"/> which reads from an existing <see cref="PipeReader"/>
/// and forwards the data to the transport layer.
/// </summary>
public class PostDataPipeReaderTests : IClassFixture<MockElasticsearchServer>
{
	private readonly MockElasticsearchServer _server;
	private readonly ITransport _transport;

	public PostDataPipeReaderTests(MockElasticsearchServer server)
	{
		_server = server;
		// Disable HTTP compression to get uncompressed JSON responses for easier testing
		var config = new TransportConfiguration(_server.Uri) { EnableHttpCompression = false };
		_transport = new DistributedTransport(config);
	}

	[Fact]
	public async Task PipeReaderForwardsDataToServer()
	{
		// Arrange - Create a Pipe and write data to it (simulating Request.BodyReader)
		var pipe = new Pipe();
		var testDocument = new { title = "Test Document", content = "Hello World" };
		var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(testDocument);

		// Write to the pipe (simulating incoming request data)
		await pipe.Writer.WriteAsync(jsonBytes);
		await pipe.Writer.CompleteAsync();

		// Act - Use PostData.PipeReader to forward the data
		var postData = PostData.PipeReader(pipe.Reader);
		var response = await _transport.RequestAsync<StringResponse>(
			HttpMethod.POST,
			"/echo",
			postData);

		// Assert
		response.ApiCallDetails.HasSuccessfulStatusCode.Should().BeTrue();
		response.Body.Should().NotBeNullOrEmpty();

		// The echo endpoint should return our data back
		var responseDoc = JsonSerializer.Deserialize<JsonElement>(response.Body);
		responseDoc.GetProperty("title").GetString().Should().Be("Test Document");
		responseDoc.GetProperty("content").GetString().Should().Be("Hello World");
	}

	[Fact]
	public async Task PipeReaderForwardsLargeDataToServer()
	{
		// Arrange - Create a larger payload
		var pipe = new Pipe();
		var largeContent = new string('x', 10_000); // 10KB - smaller to avoid potential backpressure issues
		var testDocument = new { title = "Large Document", content = largeContent };
		var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(testDocument);

		// Write data in a way that avoids potential backpressure deadlocks
		var memory = pipe.Writer.GetMemory(jsonBytes.Length);
		jsonBytes.CopyTo(memory);
		pipe.Writer.Advance(jsonBytes.Length);
		await pipe.Writer.FlushAsync();
		await pipe.Writer.CompleteAsync();

		// Act
		var postData = PostData.PipeReader(pipe.Reader);
		var response = await _transport.RequestAsync<StringResponse>(
			HttpMethod.POST,
			"/echo",
			postData);

		// Assert
		response.ApiCallDetails.HasSuccessfulStatusCode.Should().BeTrue();
		var responseDoc = JsonSerializer.Deserialize<JsonElement>(response.Body);
		responseDoc.GetProperty("content").GetString().Should().HaveLength(10_000);
	}

	[Fact]
	public async Task PipeReaderWorksWithIndexEndpoint()
	{
		// Arrange
		var pipe = new Pipe();
		var testDocument = new { title = "Indexed Document", author = "Test Author" };
		var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(testDocument);

		await pipe.Writer.WriteAsync(jsonBytes);
		await pipe.Writer.CompleteAsync();

		// Act
		var postData = PostData.PipeReader(pipe.Reader);
		var response = await _transport.RequestAsync<StringResponse>(
			HttpMethod.POST,
			"/test-index/_doc",
			postData);

		// Assert
		response.ApiCallDetails.HasSuccessfulStatusCode.Should().BeTrue();
		response.ApiCallDetails.HttpStatusCode.Should().Be(201);

		var responseDoc = JsonSerializer.Deserialize<JsonElement>(response.Body);
		responseDoc.GetProperty("_index").GetString().Should().Be("test-index");
		responseDoc.GetProperty("result").GetString().Should().Be("created");
	}

	[Fact]
	public async Task PipeReaderWorksWithChunkedWrites()
	{
		// Arrange - Write data in chunks (simulating streaming request)
		var pipe = new Pipe();
		var parts = new[]
		{
			"{\"title\":\"Chunked",
			" Document\",",
			"\"content\":\"Assembled",
			" from chunks\"}"
		};

		// Write in chunks
		foreach (var part in parts)
		{
			await pipe.Writer.WriteAsync(Encoding.UTF8.GetBytes(part));
			await pipe.Writer.FlushAsync();
		}
		await pipe.Writer.CompleteAsync();

		// Act
		var postData = PostData.PipeReader(pipe.Reader);
		var response = await _transport.RequestAsync<StringResponse>(
			HttpMethod.POST,
			"/echo",
			postData);

		// Assert
		response.ApiCallDetails.HasSuccessfulStatusCode.Should().BeTrue();
		var responseDoc = JsonSerializer.Deserialize<JsonElement>(response.Body);
		responseDoc.GetProperty("title").GetString().Should().Be("Chunked Document");
		responseDoc.GetProperty("content").GetString().Should().Be("Assembled from chunks");
	}

	[Fact]
	public async Task PipeReaderWithDisableDirectStreamingCapturesBytes()
	{
		// Arrange
		var pipe = new Pipe();
		var testDocument = new { title = "Buffered Document" };
		var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(testDocument);

		await pipe.Writer.WriteAsync(jsonBytes);
		await pipe.Writer.CompleteAsync();

		var config = new TransportConfiguration(_server.Uri)
		{
			DisableDirectStreaming = true
		};
		var transport = new DistributedTransport(config);

		// Act
		var postData = PostData.PipeReader(pipe.Reader);
		var response = await transport.RequestAsync<StringResponse>(
			HttpMethod.POST,
			"/echo",
			postData);

		// Assert
		response.ApiCallDetails.HasSuccessfulStatusCode.Should().BeTrue();
		response.ApiCallDetails.RequestBodyInBytes.Should().NotBeNull();
		response.ApiCallDetails.RequestBodyInBytes.Should().BeEquivalentTo(jsonBytes);
	}

	[Fact]
	public void PipeReaderThrowsOnNullPipeReader()
	{
		// Act & Assert
		var act = () => PostData.PipeReader(null!);
		act.Should().Throw<ArgumentNullException>();
	}

	[Fact]
	public async Task PipeReaderStreamsLargeDataConcurrentlyWithTransport()
	{
		// Arrange - This test simulates a real ASP.NET Core scenario where
		// Request.BodyReader receives data as the client sends it.
		// The transport starts reading while we're still writing.
		var pipe = new Pipe();
		const int chunkSize = 1000;
		const int totalChunks = 50; // 50KB total

		// Create the PostData before starting to write - simulates how ASP.NET Core works
		var postData = PostData.PipeReader(pipe.Reader);

		// Start the transport request - it will begin reading from the pipe
		var transportTask = _transport.RequestAsync<StringResponse>(
			HttpMethod.POST,
			"/echo",
			postData);

		// Write data in chunks concurrently with the transport reading
		// This simulates data arriving from a client over time
		var writeTask = Task.Run(async () =>
		{
			var chunk = Encoding.UTF8.GetBytes(new string('z', chunkSize));

			// Write opening JSON
			await pipe.Writer.WriteAsync("{\"title\":\"Streaming\",\"content\":\""u8.ToArray());

			// Write content in chunks with small delays to simulate network
			for (var i = 0; i < totalChunks; i++)
			{
				await pipe.Writer.WriteAsync(chunk);
				await pipe.Writer.FlushAsync();

				// Small delay to simulate data arriving over time
				if (i % 10 == 0)
					await Task.Delay(1);
			}

			// Write closing JSON
			await pipe.Writer.WriteAsync("\"}"u8.ToArray());
			await pipe.Writer.FlushAsync();

			// Complete the writer only after all data is sent
			await pipe.Writer.CompleteAsync();
		});

		// Wait for both tasks to complete
		await Task.WhenAll(writeTask, transportTask);
		var response = await transportTask;

		// Assert
		response.ApiCallDetails.HasSuccessfulStatusCode.Should().BeTrue();
		var responseDoc = JsonSerializer.Deserialize<JsonElement>(response.Body);
		responseDoc.GetProperty("title").GetString().Should().Be("Streaming");
		responseDoc.GetProperty("content").GetString().Should().HaveLength(chunkSize * totalChunks);
	}
}
