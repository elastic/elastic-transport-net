// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

#nullable enable

using System;
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
	private static readonly char[] NewLineSeparator = ['\n'];

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

		var reflectionPostData = PostData.Serializable(document);
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

	[Fact]
	public void MultiJsonTypeInfoOverloadSerializesCorrectly()
	{
		var documents = new[]
		{
			new TestDocument { Name = "first", Value = 1 },
			new TestDocument { Name = "second", Value = 2 }
		};
		var postData = PostData.MultiJson(documents, TestSerializerContext.Default.TestDocument);

		using var stream = new MemoryStream();
		postData.Write(stream, Settings, false);

		var output = Encoding.UTF8.GetString(stream.ToArray());
		var lines = output.Split(NewLineSeparator, StringSplitOptions.RemoveEmptyEntries);

		_ = lines.Should().HaveCount(2);

		var first = JsonDocument.Parse(lines[0]);
		_ = first.RootElement.GetProperty("name").GetString().Should().Be("first");
		_ = first.RootElement.GetProperty("value").GetInt32().Should().Be(1);

		var second = JsonDocument.Parse(lines[1]);
		_ = second.RootElement.GetProperty("name").GetString().Should().Be("second");
		_ = second.RootElement.GetProperty("value").GetInt32().Should().Be(2);
	}

	[Fact]
	public async Task MultiJsonTypeInfoOverloadSerializesCorrectlyAsync()
	{
		var documents = new[]
		{
			new TestDocument { Name = "async-first", Value = 10 },
			new TestDocument { Name = "async-second", Value = 20 }
		};
		var postData = PostData.MultiJson(documents, TestSerializerContext.Default.TestDocument);

		using var stream = new MemoryStream();
		await postData.WriteAsync(stream, Settings, false, CancellationToken.None);

		var output = Encoding.UTF8.GetString(stream.ToArray());
		var lines = output.Split(NewLineSeparator, StringSplitOptions.RemoveEmptyEntries);

		_ = lines.Should().HaveCount(2);

		var first = JsonDocument.Parse(lines[0]);
		_ = first.RootElement.GetProperty("name").GetString().Should().Be("async-first");

		var second = JsonDocument.Parse(lines[1]);
		_ = second.RootElement.GetProperty("name").GetString().Should().Be("async-second");
	}

	[Fact]
	public void MultiJsonTypeInfoOverloadSetsPostType()
	{
		var postData = PostData.MultiJson(new[] { new TestDocument() }, TestSerializerContext.Default.TestDocument);
		_ = postData.Type.Should().Be(PostType.EnumerableOfObject);
	}

#if NET8_0_OR_GREATER
	[Fact]
	public void SerializerThrowsForUnregisteredTypeWhenReflectionDisabled()
	{
		// Create a serializer with a context that does NOT include UnregisteredType
		var options = new JsonSerializerOptions
		{
			TypeInfoResolver = TestSerializerContext.Default
		};
		var serializer = new TestSystemTextJsonSerializer(options);

		using var stream = new MemoryStream();
		var act = () => serializer.Serialize(new UnregisteredType { Data = "test" }, stream);

		_ = act.Should().Throw<InvalidOperationException>()
			.WithMessage("*UnregisteredType*not registered*");
	}

	[Fact]
	public async Task SerializerThrowsForUnregisteredTypeWhenReflectionDisabledAsync()
	{
		var options = new JsonSerializerOptions
		{
			TypeInfoResolver = TestSerializerContext.Default
		};
		var serializer = new TestSystemTextJsonSerializer(options);

		using var stream = new MemoryStream();
		var act = () => serializer.SerializeAsync(new UnregisteredType { Data = "test" }, stream);

		_ = await act.Should().ThrowAsync<InvalidOperationException>()
			.WithMessage("*UnregisteredType*not registered*");
	}
#endif
}

public sealed class TestDocument
{
	[JsonPropertyName("name")]
	public string Name { get; set; } = string.Empty;

	[JsonPropertyName("value")]
	public int Value { get; set; }
}

public sealed class UnregisteredType
{
	[JsonPropertyName("data")]
	public string Data { get; set; } = string.Empty;
}

[JsonSerializable(typeof(TestDocument))]
internal sealed partial class TestSerializerContext : JsonSerializerContext;

internal sealed class TestSystemTextJsonSerializer : SystemTextJsonSerializer
{
	internal TestSystemTextJsonSerializer(JsonSerializerOptions options) : base(new FixedOptionsProvider(options)) { }

	private sealed class FixedOptionsProvider : IJsonSerializerOptionsProvider
	{
		private readonly JsonSerializerOptions _options;

		public FixedOptionsProvider(JsonSerializerOptions options) => _options = options;

		public JsonSerializerOptions CreateJsonSerializerOptions() => _options;
	}
}
