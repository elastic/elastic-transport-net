// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.IO;
using System.Text.Json;
using System;
using System.Text.Json.Nodes;

namespace Elastic.Transport.Extensions;

/// <summary>
/// A set of handy extension methods for <see cref="Serializer"/>
/// </summary>
public static class TransportSerializerExtensions
{
	/// <summary>
	/// Extension method that serializes an instance of <typeparamref name="T"/> to a byte array.
	/// </summary>
	public static byte[] SerializeToBytes<T>(
		this Serializer serializer,
		T data,
		SerializationFormatting formatting = SerializationFormatting.None) =>
		SerializeToBytes(serializer, data, TransportConfiguration.DefaultMemoryStreamFactory, formatting);

	/// <summary>
	/// Extension method that serializes an instance of <typeparamref name="T"/> to a byte array.
	/// </summary>
	/// <param name="data"></param>
	/// <param name="memoryStreamFactory">
	/// A factory yielding MemoryStream instances, defaults to <see cref="RecyclableMemoryStreamFactory"/>
	/// that yields memory streams backed by pooled byte arrays.
	/// </param>
	/// <param name="serializer"><inheritdoc cref="Serializer" path="/summary"/></param>
	/// <param name="formatting"><inheritdoc cref="SerializationFormatting" path="/summary"/></param>
	public static byte[] SerializeToBytes<T>(
		this Serializer serializer,
		T data,
		MemoryStreamFactory? memoryStreamFactory = null,
		SerializationFormatting formatting = SerializationFormatting.None
	)
	{
		memoryStreamFactory ??= TransportConfiguration.DefaultMemoryStreamFactory;
		using var ms = memoryStreamFactory.Create();
		serializer.Serialize(data, ms, formatting);
		return ms.ToArray();
	}

	/// <summary>
	/// Extension method that serializes an instance of <typeparamref name="T"/> to a string.
	/// </summary>
	public static string SerializeToString<T>(
		this Serializer serializer,
		T data,
		SerializationFormatting formatting = SerializationFormatting.None) =>
		SerializeToString(serializer, data, TransportConfiguration.DefaultMemoryStreamFactory, formatting);

	/// <summary>
	/// Extension method that serializes an instance of <typeparamref name="T"/> to a string.
	/// </summary>
	/// <param name="data"></param>
	/// <param name="memoryStreamFactory">
	/// A factory yielding MemoryStream instances, defaults to <see cref="RecyclableMemoryStreamFactory"/>
	/// that yields memory streams backed by pooled byte arrays.
	/// </param>
	/// <param name="serializer"><inheritdoc cref="Serializer" path="/summary"/></param>
	/// <param name="formatting"><inheritdoc cref="SerializationFormatting" path="/summary"/></param>
	public static string SerializeToString<T>(
		this Serializer serializer,
		T data,
		MemoryStreamFactory? memoryStreamFactory = null,
		SerializationFormatting formatting = SerializationFormatting.None
	)
	{
		memoryStreamFactory ??= TransportConfiguration.DefaultMemoryStreamFactory;
		using var ms = memoryStreamFactory.Create();
		serializer.Serialize(data, ms, formatting);
		return ms.Utf8String();
	}

	#region STJ Extensions

	/// <summary>
	/// Extension method that writes the serialized representation of an instance of <typeparamref name="T"/> to a
	/// <see cref="Utf8JsonWriter"/>.
	/// </summary>
	/// <typeparam name="T">The type of the data to be serialized.</typeparam>
	/// <param name="serializer"><inheritdoc cref="Serializer" path="/summary"/></param>
	/// <param name="data">The data to serialize.</param>
	/// <param name="writer">The destination <see cref="Utf8JsonWriter"/>.</param>
	/// <param name="memoryStreamFactory">
	/// A factory yielding <see cref="MemoryStream"/> instances, defaults to <see cref="RecyclableMemoryStreamFactory"/>
	/// that yields memory streams backed by pooled byte arrays.
	/// </param>
	/// <param name="formatting"><inheritdoc cref="SerializationFormatting" path="/summary"/></param>
	public static void Serialize<T>(
		this Serializer serializer,
		T? data,
		Utf8JsonWriter writer,
		MemoryStreamFactory? memoryStreamFactory = null,
		SerializationFormatting formatting = SerializationFormatting.None)
	{
		if (serializer is SystemTextJsonSerializer stjSerializer)
		{
			// When the serializer derives from `SystemTextJsonSerializer` we can avoid unnecessary allocations and
			// serialize straight into the writer.
			JsonSerializer.Serialize(writer, data, stjSerializer.GetJsonSerializerOptions(formatting));
			return;
		}

		memoryStreamFactory ??= TransportConfiguration.DefaultMemoryStreamFactory;
		using var ms = memoryStreamFactory.Create();

		serializer.Serialize(data, ms);
		ms.Position = 0;

#if NET6_0_OR_GREATER
		writer.WriteRawValue(ms.GetBuffer().AsSpan()[..(int)ms.Length], true);
#else
		using var document = JsonDocument.Parse(ms);
		document.RootElement.WriteTo(writer);
#endif
	}

	/// <summary>
	/// Extension method that writes the serialized representation of the given <paramref name="data"/> to a
	/// <see cref="Utf8JsonWriter"/>.
	/// </summary>
	/// <param name="serializer"><inheritdoc cref="Serializer" path="/summary"/></param>
	/// <param name="data">The data to serialize.</param>
	/// <param name="type">The type of the data to serialize.</param>
	/// <param name="writer">The destination <see cref="Utf8JsonWriter"/>.</param>
	/// <param name="memoryStreamFactory">
	/// A factory yielding <see cref="MemoryStream"/> instances, defaults to <see cref="RecyclableMemoryStreamFactory"/>
	/// that yields memory streams backed by pooled byte arrays.
	/// </param>
	/// <param name="formatting"><inheritdoc cref="SerializationFormatting" path="/summary"/></param>
	public static void Serialize(
		this Serializer serializer,
		object? data,
		Type type,
		Utf8JsonWriter writer,
		MemoryStreamFactory? memoryStreamFactory = null,
		SerializationFormatting formatting = SerializationFormatting.None)
	{
		if (serializer is SystemTextJsonSerializer stjSerializer)
		{
			// When the serializer derives from `SystemTextJsonSerializer` we can avoid unnecessary allocations and
			// serialize straight into the writer.
			JsonSerializer.Serialize(writer, data, type, stjSerializer.GetJsonSerializerOptions(formatting));
			return;
		}

		memoryStreamFactory ??= TransportConfiguration.DefaultMemoryStreamFactory;
		using var ms = memoryStreamFactory.Create();

		serializer.Serialize(data, ms);
		ms.Position = 0;

#if NET6_0_OR_GREATER
		writer.WriteRawValue(ms.GetBuffer().AsSpan()[..(int)ms.Length], true);
#else
		using var document = JsonDocument.Parse(ms);
		document.RootElement.WriteTo(writer);
#endif
	}

	/// <summary>
	/// Extension method that deserializes from a given <see cref="Utf8JsonReader"/>.
	/// </summary>
	/// <typeparam name="T">The type of the data to be deserialized.</typeparam>
	/// <param name="serializer"><inheritdoc cref="Serializer" path="/summary"/></param>
	/// <param name="reader">The source <see cref="Utf8JsonReader"/></param>
	/// <param name="memoryStreamFactory">
	/// A factory yielding <see cref="MemoryStream"/> instances, defaults to <see cref="RecyclableMemoryStreamFactory"/>
	/// that yields memory streams backed by pooled byte arrays.
	/// </param>
	/// <returns>The deserialized data.</returns>
	public static T? Deserialize<T>(
		this Serializer serializer,
		ref Utf8JsonReader reader,
		MemoryStreamFactory? memoryStreamFactory = null)
	{
		if (serializer is SystemTextJsonSerializer stjSerializer)
		{
			// When the serializer derives from `SystemTextJsonSerializer` we can avoid unnecessary allocations and
			// deserialize straight from the reader.
			return JsonSerializer.Deserialize<T>(ref reader, stjSerializer.GetJsonSerializerOptions(SerializationFormatting.None));
		}

		using var jsonDoc = JsonSerializer.Deserialize<JsonDocument>(ref reader);

		memoryStreamFactory ??= TransportConfiguration.DefaultMemoryStreamFactory;
		using var ms = memoryStreamFactory.Create();

		var writer = new Utf8JsonWriter(ms);
		jsonDoc.WriteTo(writer);
		writer.Flush();
		ms.Position = 0;

		return serializer.Deserialize<T>(ms);
	}

	/// <summary>
	/// Extension method that deserializes from a given <see cref="Utf8JsonReader"/>.
	/// </summary>
	/// <param name="serializer"><inheritdoc cref="Serializer" path="/summary"/></param>
	/// <param name="reader">The source <see cref="Utf8JsonReader"/></param>
	/// <param name="type">The type of the data to be deserialized.</param>
	/// <param name="memoryStreamFactory">
	/// A factory yielding <see cref="MemoryStream"/> instances, defaults to <see cref="RecyclableMemoryStreamFactory"/>
	/// that yields memory streams backed by pooled byte arrays.
	/// </param>
	/// <returns>The deserialized data.</returns>
	public static object? Deserialize(
		this Serializer serializer,
		ref Utf8JsonReader reader,
		Type type,
		MemoryStreamFactory? memoryStreamFactory = null)
	{
		if (serializer is SystemTextJsonSerializer stjSerializer)
		{
			// When the serializer derives from `SystemTextJsonSerializer` we can avoid unnecessary allocations and
			// deserialize straight from the reader.
			return JsonSerializer.Deserialize(ref reader, type, stjSerializer.GetJsonSerializerOptions(SerializationFormatting.None));
		}

		using var jsonDoc = JsonSerializer.Deserialize<JsonDocument>(ref reader);

		memoryStreamFactory ??= TransportConfiguration.DefaultMemoryStreamFactory;
		using var ms = memoryStreamFactory.Create();

		var writer = new Utf8JsonWriter(ms);
		jsonDoc.WriteTo(writer);
		writer.Flush();
		ms.Position = 0;

		return serializer.Deserialize(type, ms);
	}

	/// <summary>
	/// Extension method that deserializes from a given <see cref="JsonNode"/>.
	/// </summary>
	/// <typeparam name="T">The type of the data to be deserialized.</typeparam>
	/// <param name="serializer"><inheritdoc cref="Serializer" path="/summary"/></param>
	/// <param name="node">The source <see cref="JsonNode"/></param>
	/// <param name="memoryStreamFactory">
	/// A factory yielding <see cref="MemoryStream"/> instances, defaults to <see cref="RecyclableMemoryStreamFactory"/>
	/// that yields memory streams backed by pooled byte arrays.
	/// </param>
	/// <returns>The deserialized data.</returns>
	public static T? Deserialize<T>(
		this Serializer serializer,
		JsonNode node,
		MemoryStreamFactory? memoryStreamFactory = null)
	{
		if (serializer is SystemTextJsonSerializer stjSerializer)
		{
			// When the serializer derives from `SystemTextJsonSerializer` we can avoid unnecessary allocations and
			// deserialize straight from the node.
			return node.Deserialize<T>(stjSerializer.GetJsonSerializerOptions(SerializationFormatting.None));
		}

		memoryStreamFactory ??= TransportConfiguration.DefaultMemoryStreamFactory;
		using var ms = memoryStreamFactory.Create();

		using var writer = new Utf8JsonWriter(ms);
		node.WriteTo(writer);
		writer.Flush();
		ms.Position = 0;

		return serializer.Deserialize<T>(ms);
	}

	/// <summary>
	/// Extension method that deserializes from a given <see cref="JsonNode"/>.
	/// </summary>
	/// <param name="serializer"><inheritdoc cref="Serializer" path="/summary"/></param>
	/// <param name="node">The source <see cref="JsonNode"/></param>
	/// <param name="type">The type of the data to be deserialized.</param>
	/// <param name="memoryStreamFactory">
	/// A factory yielding <see cref="MemoryStream"/> instances, defaults to <see cref="RecyclableMemoryStreamFactory"/>
	/// that yields memory streams backed by pooled byte arrays.
	/// </param>
	/// <returns>The deserialized data.</returns>
	public static object? Deserialize(
		this Serializer serializer,
		JsonNode node,
		Type type,
		MemoryStreamFactory? memoryStreamFactory = null)
	{
		if (serializer is SystemTextJsonSerializer stjSerializer)
		{
			// When the serializer derives from `SystemTextJsonSerializer` we can avoid unnecessary allocations and
			// deserialize straight from the node.
			return node.Deserialize(type, stjSerializer.GetJsonSerializerOptions(SerializationFormatting.None));
		}

		memoryStreamFactory ??= TransportConfiguration.DefaultMemoryStreamFactory;
		using var ms = memoryStreamFactory.Create();

		using var writer = new Utf8JsonWriter(ms);
		node.WriteTo(writer);
		writer.Flush();
		ms.Position = 0;

		return serializer.Deserialize(type, ms);
	}

	/// <summary>
	/// Extension method that deserializes from a given <see cref="JsonElement"/>.
	/// </summary>
	/// <typeparam name="T">The type of the data to be deserialized.</typeparam>
	/// <param name="serializer"><inheritdoc cref="Serializer" path="/summary"/></param>
	/// <param name="node">The source <see cref="JsonElement"/></param>
	/// <param name="memoryStreamFactory">
	/// A factory yielding <see cref="MemoryStream"/> instances, defaults to <see cref="RecyclableMemoryStreamFactory"/>
	/// that yields memory streams backed by pooled byte arrays.
	/// </param>
	/// <returns>The deserialized data.</returns>
	public static T? Deserialize<T>(
		this Serializer serializer,
		JsonElement node,
		MemoryStreamFactory? memoryStreamFactory = null)
	{
		if (serializer is SystemTextJsonSerializer stjSerializer)
		{
			// When the serializer derives from `SystemTextJsonSerializer` we can avoid unnecessary allocations and
			// deserialize straight from the node.
			return node.Deserialize<T>(stjSerializer.GetJsonSerializerOptions(SerializationFormatting.None));
		}

		memoryStreamFactory ??= TransportConfiguration.DefaultMemoryStreamFactory;
		using var ms = memoryStreamFactory.Create();

		using var writer = new Utf8JsonWriter(ms);
		node.WriteTo(writer);
		writer.Flush();
		ms.Position = 0;

		return serializer.Deserialize<T>(ms);
	}

	/// <summary>
	/// Extension method that deserializes from a given <see cref="JsonElement"/>.
	/// </summary>
	/// <param name="serializer"><inheritdoc cref="Serializer" path="/summary"/></param>
	/// <param name="node">The source <see cref="JsonElement"/></param>
	/// <param name="type">The type of the data to be deserialized.</param>
	/// <param name="memoryStreamFactory">
	/// A factory yielding <see cref="MemoryStream"/> instances, defaults to <see cref="RecyclableMemoryStreamFactory"/>
	/// that yields memory streams backed by pooled byte arrays.
	/// </param>
	/// <returns>The deserialized data.</returns>
	public static object? Deserialize(
		this Serializer serializer,
		JsonElement node,
		Type type,
		MemoryStreamFactory? memoryStreamFactory = null)
	{
		if (serializer is SystemTextJsonSerializer stjSerializer)
		{
			// When the serializer derives from `SystemTextJsonSerializer` we can avoid unnecessary allocations and
			// deserialize straight from the node.
			return node.Deserialize(type, stjSerializer.GetJsonSerializerOptions(SerializationFormatting.None));
		}

		memoryStreamFactory ??= TransportConfiguration.DefaultMemoryStreamFactory;
		using var ms = memoryStreamFactory.Create();

		using var writer = new Utf8JsonWriter(ms);
		node.WriteTo(writer);
		writer.Flush();
		ms.Position = 0;

		return serializer.Deserialize(type, ms);
	}

	#endregion STJ Extensions
}
