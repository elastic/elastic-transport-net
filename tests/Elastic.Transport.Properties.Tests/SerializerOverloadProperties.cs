// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CsCheck;
using Elastic.Transport.Extensions;
using FluentAssertions;
using Xunit;

namespace Elastic.Transport.Properties.Tests;

/// <summary>
/// TransportSerializerExtensions has 25 overloads that are the same operation spelled for different inputs and outputs. They
/// must all agree. This is the safety net that has to exist before any of them is merged or generated: the day two of them
/// disagree, this says which.
/// </summary>
public class SerializerOverloadProperties
{
	/// <summary>
	/// Only the stream members, so it is not a SystemTextJsonSerializer: every extension takes its generic fallback through a
	/// stream, the path that the built-in serializer's fast path skips entirely.
	/// </summary>
	private sealed class StreamOnlySerializer(Serializer inner) : Serializer
	{
		public override object? Deserialize(Type type, Stream stream) => inner.Deserialize(type, stream);

		public override T Deserialize<T>(Stream stream) => inner.Deserialize<T>(stream);

		public override System.Threading.Tasks.ValueTask<object?> DeserializeAsync(Type type, Stream stream, System.Threading.CancellationToken cancellationToken = default) =>
			inner.DeserializeAsync(type, stream, cancellationToken);

		public override System.Threading.Tasks.ValueTask<T> DeserializeAsync<T>(Stream stream, System.Threading.CancellationToken cancellationToken = default) =>
			inner.DeserializeAsync<T>(stream, cancellationToken);

		public override void Serialize(object? data, Type type, Stream stream, SerializationFormatting formatting = SerializationFormatting.None, System.Threading.CancellationToken cancellationToken = default) =>
			inner.Serialize(data, type, stream, formatting, cancellationToken);

		public override void Serialize<T>(T data, Stream stream, SerializationFormatting formatting = SerializationFormatting.None) =>
			inner.Serialize(data, stream, formatting);

		public override System.Threading.Tasks.Task SerializeAsync(object? data, Type type, Stream stream, SerializationFormatting formatting = SerializationFormatting.None, System.Threading.CancellationToken cancellationToken = default) =>
			inner.SerializeAsync(data, type, stream, formatting, cancellationToken);

		public override System.Threading.Tasks.Task SerializeAsync<T>(T data, Stream stream, SerializationFormatting formatting = SerializationFormatting.None, System.Threading.CancellationToken cancellationToken = default) =>
			inner.SerializeAsync(data, stream, formatting, cancellationToken);
	}

	private static readonly Serializer BuiltIn = LowLevelRequestResponseSerializer.Instance;

	private static readonly (string Name, Serializer Serializer)[] Serializers =
		[("built-in", BuiltIn), ("stream fallback", new StreamOnlySerializer(BuiltIn))];

	// Characters JSON must escape, plus multi-byte ones, but no lone surrogates (not representable in valid UTF-8 JSON).
	private static readonly Gen<string> Text =
		Gen.Int[0x20, 0xD7FF].Select(i => (char)i).Array[0, 30].Select(chars => new string(chars));

	private static readonly Gen<JsonObject> Json =
		from i in Gen.Int
		from s in Text
		from b in Gen.Bool
		from numbers in Gen.Int[-1000, 1000].Array[0, 6]
		from d in Gen.Int[-100000, 100000].Select(n => n / 100.0)
		from nestedText in Text
		select new JsonObject
		{
			["i"] = i,
			["s"] = s,
			["b"] = b,
			["a"] = new JsonArray(numbers.Select(n => (JsonNode?)n).ToArray()),
			["nothing"] = null,
			["o"] = new JsonObject { ["x"] = nestedText, ["y"] = d },
		};

	private static void Same(JsonNode? actual, JsonNode expected, string overload) =>
		_ = JsonNode.DeepEquals(actual, expected).Should().BeTrue($"{overload} gave {actual?.ToJsonString()}, expected {expected.ToJsonString()}");

	[Fact]
	public void EveryDeserializeOverloadReadsTheSameJsonTheSameWay() =>
		Json.Sample(expected =>
		{
			var text = expected.ToJsonString();
			var bytes = Encoding.UTF8.GetBytes(text);
			var factory = DefaultMemoryStreamFactory.Default;
			using var document = JsonDocument.Parse(text);

			foreach (var (name, serializer) in Serializers)
			{
				Same(serializer.Deserialize<JsonNode>(text), expected, $"{name}: string");
				Same(serializer.Deserialize<JsonNode>(text, factory), expected, $"{name}: string + factory");
				Same(serializer.Deserialize<JsonNode>(bytes.AsSpan()), expected, $"{name}: ReadOnlySpan<byte>");
				Same(serializer.Deserialize<JsonNode>(text.AsSpan()), expected, $"{name}: ReadOnlySpan<char>");
				Same(serializer.Deserialize<JsonNode>(JsonNode.Parse(text)!), expected, $"{name}: JsonNode");
				Same(serializer.Deserialize<JsonNode>(document.RootElement), expected, $"{name}: JsonElement");

				var reader = new Utf8JsonReader(bytes);
				Same(serializer.Deserialize<JsonNode>(ref reader), expected, $"{name}: Utf8JsonReader");

				// The Type flavours are the same operation with the type given at run time. They are what is being tested, so the
				// analyzer's advice to use the generic overload instead is exactly what must not happen here.
#pragma warning disable CA2263
				Same((JsonNode?)serializer.Deserialize(text, typeof(JsonNode)), expected, $"{name}: string, Type");
				Same((JsonNode?)serializer.Deserialize(bytes.AsSpan(), typeof(JsonNode)), expected, $"{name}: ReadOnlySpan<byte>, Type");
				Same((JsonNode?)serializer.Deserialize(text.AsSpan(), typeof(JsonNode)), expected, $"{name}: ReadOnlySpan<char>, Type");
				Same((JsonNode?)serializer.Deserialize(JsonNode.Parse(text)!, typeof(JsonNode)), expected, $"{name}: JsonNode, Type");
				Same((JsonNode?)serializer.Deserialize(document.RootElement, typeof(JsonNode)), expected, $"{name}: JsonElement, Type");
#pragma warning restore CA2263
			}
		}, iter: 300);

	[Fact]
	public void EverySerializeOverloadWritesTheSameJson() =>
		Json.Sample(node =>
		{
			var factory = DefaultMemoryStreamFactory.Default;
			var expectedText = BuiltIn.SerializeToString(node);

			foreach (var (name, serializer) in Serializers)
			{
				var text = serializer.SerializeToString(node);
				var bytes = serializer.SerializeToBytes(node);

				_ = text.Should().Be(expectedText, $"{name}: the same text as the built-in serializer");
				_ = Encoding.UTF8.GetString(bytes).Should().Be(text, $"{name}: SerializeToBytes is SerializeToString, encoded");
				_ = serializer.SerializeToString(node, factory).Should().Be(text, $"{name}: string + factory");
				_ = serializer.SerializeToBytes(node, factory).Should().Equal(bytes, $"{name}: bytes + factory");
				_ = serializer.SerializeToString((object?)node, typeof(JsonNode)).Should().Be(text, $"{name}: string, Type");
				_ = serializer.SerializeToBytes((object?)node, typeof(JsonNode)).Should().Equal(bytes, $"{name}: bytes, Type");

				using var stream = new MemoryStream();
				using (var writer = new Utf8JsonWriter(stream))
					serializer.Serialize(node, writer);
				_ = stream.ToArray().Should().Equal(bytes, $"{name}: writing through a Utf8JsonWriter");
			}
		}, iter: 300);

	[Fact]
	public void SerializingThenDeserializingGivesTheValueBackWhateverTheFormatting() =>
		Json.Sample(node =>
		{
			foreach (var formatting in new[] { SerializationFormatting.None, SerializationFormatting.Indented })
			{
				foreach (var (name, serializer) in Serializers)
					Same(serializer.Deserialize<JsonNode>(serializer.SerializeToString(node, formatting)), node, $"{name}: round trip, {formatting}");
			}
		}, iter: 300);

	[Fact]
	public void IndentedAndCompactJsonDiffOnlyInWhitespace() =>
		Json.Sample(node =>
		{
			var compact = BuiltIn.SerializeToString(node, SerializationFormatting.None);
			var indented = BuiltIn.SerializeToString(node, SerializationFormatting.Indented);

			_ = indented.Should().Contain("\n", "indented output spans lines");
			_ = compact.Should().NotContain("\n");
			Same(JsonNode.Parse(indented), JsonNode.Parse(compact)!, "indented vs compact");
		}, iter: 200);
}
