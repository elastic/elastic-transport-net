// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

#nullable enable

using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;
using Elastic.Transport.Tests.Plumbing;
using FluentAssertions;
using Xunit;

namespace Elastic.Transport.Tests;

public class PostDataSerializableTests
{
	private static readonly TransportConfiguration Settings = InMemoryConnectionFactory.Create();

	[Fact]
	public void TypeInfoOverloadSerializesCorrectly()
	{
		var document = new TestDocument { Name = "test", Value = 42 };
		var postData = PostData.Serializable(document, TestSerializerContext.Default.TestDocument);

		using var stream = new MemoryStream();
		postData.Write(stream, Settings, false);

		var json = Encoding.UTF8.GetString(stream.ToArray());
		var parsed = JsonDocument.Parse(json);

		_ = parsed.RootElement.GetProperty("name").GetString().Should().Be("test");
		_ = parsed.RootElement.GetProperty("value").GetInt32().Should().Be(42);
	}

	[Fact]
	public async Task TypeInfoOverloadSerializesCorrectlyAsync()
	{
		var document = new TestDocument { Name = "async-test", Value = 99 };
		var postData = PostData.Serializable(document, TestSerializerContext.Default.TestDocument);

		using var stream = new MemoryStream();
		await postData.WriteAsync(stream, Settings, false, CancellationToken.None);

		var json = Encoding.UTF8.GetString(stream.ToArray());
		var parsed = JsonDocument.Parse(json);

		_ = parsed.RootElement.GetProperty("name").GetString().Should().Be("async-test");
		_ = parsed.RootElement.GetProperty("value").GetInt32().Should().Be(99);
	}

	[Fact]
	public void TypeInfoOverloadCapturesWrittenBytesWhenDirectStreamingDisabled()
	{
		var document = new TestDocument { Name = "buffered", Value = 1 };
		var postData = PostData.Serializable(document, TestSerializerContext.Default.TestDocument);

		using var stream = new MemoryStream();
		postData.Write(stream, Settings, disableDirectStreaming: true);

		postData.WrittenBytes.Should().NotBeNull();

		var json = Encoding.UTF8.GetString(postData.WrittenBytes!);
		_ = json.Should().Contain("\"name\":\"buffered\"");
	}

	[Fact]
	public void TypeInfoOverloadSetsPostTypeToSerializable()
	{
		var postData = PostData.Serializable(new TestDocument(), TestSerializerContext.Default.TestDocument);
		_ = postData.Type.Should().Be(PostType.Serializable);
	}

	[Fact]
	public void TypeInfoOverloadProducesSameOutputAsReflectionOverload()
	{
		var document = new TestDocument { Name = "compare", Value = 7 };

		var typeInfoPostData = PostData.Serializable(document, TestSerializerContext.Default.TestDocument);
		using var typeInfoStream = new MemoryStream();
		typeInfoPostData.Write(typeInfoStream, Settings, false);

#pragma warning disable IL2026, IL3050
		var reflectionPostData = PostData.Serializable(document);
#pragma warning restore IL2026, IL3050
		using var reflectionStream = new MemoryStream();
		reflectionPostData.Write(reflectionStream, Settings, false);

		var typeInfoJson = Encoding.UTF8.GetString(typeInfoStream.ToArray());
		var reflectionJson = Encoding.UTF8.GetString(reflectionStream.ToArray());

		var typeInfoDoc = JsonDocument.Parse(typeInfoJson);
		var reflectionDoc = JsonDocument.Parse(reflectionJson);

		_ = typeInfoDoc.RootElement.GetProperty("name").GetString().Should().Be("compare");
		_ = reflectionDoc.RootElement.GetProperty("name").GetString().Should().Be("compare");
		_ = typeInfoDoc.RootElement.GetProperty("value").GetInt32().Should().Be(7);
		_ = reflectionDoc.RootElement.GetProperty("value").GetInt32().Should().Be(7);
	}
}

public sealed class TestDocument
{
	[JsonPropertyName("name")]
	public string Name { get; set; } = string.Empty;

	[JsonPropertyName("value")]
	public int Value { get; set; }
}

[JsonSerializable(typeof(TestDocument))]
internal sealed partial class TestSerializerContext : JsonSerializerContext;
