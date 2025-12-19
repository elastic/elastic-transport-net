// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.IO.Pipelines;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Elastic.Transport.Pipes.IntegrationTests;

/// <summary>
/// Tests for <see cref="PostData.PipeWriter{T}"/> which uses a callback to write data
/// via a <see cref="PipeWriter"/> for efficient serialization.
/// </summary>
public class PostDataPipeWriterTests : IClassFixture<MockElasticsearchServer>
{
	private readonly MockElasticsearchServer _server;
	private readonly ITransport _transport;

	public PostDataPipeWriterTests(MockElasticsearchServer server)
	{
		_server = server;
		// Create transport with compression disabled for clearer test results
		var config = new TransportConfiguration(_server.Uri) { EnableHttpCompression = false };
		_transport = new DistributedTransport(config);
	}

	[Fact]
	public async Task PipeWriterSerializesObjectToServer()
	{
		// Arrange
		var testDocument = new TestDocument { Title = "PipeWriter Test", Value = 42 };

		// Act - Use PostData.PipeWriter with JsonSerializer
		var postData = PostData.PipeWriter(testDocument, static async (doc, writer, ct) =>
		{
			await JsonSerializer.SerializeAsync(writer, doc, cancellationToken: ct);
		});

		var response = await _transport.RequestAsync<StringResponse>(
			HttpMethod.POST,
			"/echo",
			postData);

		// Assert
		response.ApiCallDetails.HasSuccessfulStatusCode.Should().BeTrue();

		var responseDoc = JsonSerializer.Deserialize<TestDocument>(response.Body);
		responseDoc.Should().NotBeNull();
		responseDoc!.Title.Should().Be("PipeWriter Test");
		responseDoc.Value.Should().Be(42);
	}

	[Fact]
	public async Task PipeWriterWorksWithIndexEndpoint()
	{
		// Arrange
		var testDocument = new TestDocument { Title = "Indexed via PipeWriter", Value = 100 };

		// Act
		var postData = PostData.PipeWriter(testDocument, static async (doc, writer, ct) =>
		{
			await JsonSerializer.SerializeAsync(writer, doc, cancellationToken: ct);
		});

		var response = await _transport.RequestAsync<StringResponse>(
			HttpMethod.POST,
			"/my-index/_doc",
			postData);

		// Assert
		response.ApiCallDetails.HasSuccessfulStatusCode.Should().BeTrue();
		response.ApiCallDetails.HttpStatusCode.Should().Be(201);

		var responseDoc = JsonSerializer.Deserialize<JsonElement>(response.Body);
		responseDoc.GetProperty("_index").GetString().Should().Be("my-index");
		responseDoc.GetProperty("result").GetString().Should().Be("created");
	}

	[Fact]
	public async Task PipeWriterSerializesLargeObject()
	{
		// Arrange
		var largeContent = new string('y', 50_000);
		var testDocument = new TestDocument { Title = "Large Document", Value = 999, Content = largeContent };

		// Act
		var postData = PostData.PipeWriter(testDocument, static async (doc, writer, ct) =>
		{
			await JsonSerializer.SerializeAsync(writer, doc, cancellationToken: ct);
		});

		var response = await _transport.RequestAsync<StringResponse>(
			HttpMethod.POST,
			"/echo",
			postData);

		// Assert
		response.ApiCallDetails.HasSuccessfulStatusCode.Should().BeTrue();

		var responseDoc = JsonSerializer.Deserialize<TestDocument>(response.Body);
		responseDoc!.Content.Should().HaveLength(50_000);
	}

	[Fact]
	public async Task PipeWriterWithDisableDirectStreamingCapturesBytes()
	{
		// Arrange
		var testDocument = new TestDocument { Title = "Buffered", Value = 1 };
		var config = new TransportConfiguration(_server.Uri)
		{
			DisableDirectStreaming = true,
			EnableHttpCompression = false
		};
		var transport = new DistributedTransport(config);

		// Act
		var postData = PostData.PipeWriter(testDocument, static async (doc, writer, ct) =>
		{
			await JsonSerializer.SerializeAsync(writer, doc, cancellationToken: ct);
		});

		var response = await transport.RequestAsync<StringResponse>(
			HttpMethod.POST,
			"/echo",
			postData);

		// Assert
		response.ApiCallDetails.HasSuccessfulStatusCode.Should().BeTrue();
		response.ApiCallDetails.RequestBodyInBytes.Should().NotBeNull();

		// Verify the captured bytes match what we serialized
		var capturedJson = System.Text.Encoding.UTF8.GetString(response.ApiCallDetails.RequestBodyInBytes!);
		var capturedDoc = JsonSerializer.Deserialize<TestDocument>(capturedJson);
		capturedDoc!.Title.Should().Be("Buffered");
	}

	[Fact]
	public async Task PipeWriterWorksWithAnonymousTypes()
	{
		// Arrange
		var anonymousDoc = new { name = "Anonymous", count = 5, active = true };

		// Act
		var postData = PostData.PipeWriter(anonymousDoc, static async (doc, writer, ct) =>
		{
			await JsonSerializer.SerializeAsync(writer, doc, cancellationToken: ct);
		});

		var response = await _transport.RequestAsync<StringResponse>(
			HttpMethod.POST,
			"/echo",
			postData);

		// Assert
		response.ApiCallDetails.HasSuccessfulStatusCode.Should().BeTrue();

		var responseDoc = JsonSerializer.Deserialize<JsonElement>(response.Body);
		responseDoc.GetProperty("name").GetString().Should().Be("Anonymous");
		responseDoc.GetProperty("count").GetInt32().Should().Be(5);
		responseDoc.GetProperty("active").GetBoolean().Should().BeTrue();
	}

	[Fact]
	public async Task PipeWriterWorksWithJsonSerializerOptions()
	{
		// Arrange
		var testDocument = new TestDocument { Title = "With Options", Value = 42 };
		var options = new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			WriteIndented = false
		};

		// Act
		var postData = PostData.PipeWriter((testDocument, options), static async (state, writer, ct) =>
		{
			await JsonSerializer.SerializeAsync(writer, state.testDocument, state.options, ct);
		});

		var response = await _transport.RequestAsync<StringResponse>(
			HttpMethod.POST,
			"/echo",
			postData);

		// Assert
		response.ApiCallDetails.HasSuccessfulStatusCode.Should().BeTrue();
		// The response will have camelCase property names
		response.Body.Should().Contain("\"title\":");
		response.Body.Should().Contain("\"value\":");
	}

	[Fact]
	public void PipeWriterThrowsOnNullAsyncWriter()
	{
		// Arrange
		var doc = new TestDocument();

		// Act & Assert
		var act = () => PostData.PipeWriter(doc, null!);
		act.Should().Throw<ArgumentNullException>();
	}

	private sealed class TestDocument
	{
		public string Title { get; set; } = string.Empty;
		public int Value { get; set; }
		public string? Content { get; set; }
	}
}
