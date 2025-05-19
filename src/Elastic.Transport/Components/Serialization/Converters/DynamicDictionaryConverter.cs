// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Elastic.Transport.Products.Elasticsearch;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Elastic.Transport;

internal class DynamicDictionaryConverter : JsonConverter<DynamicDictionary>
{
	public override DynamicDictionary Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		if (reader.TokenType == JsonTokenType.StartArray)
		{
			var array = JsonSerializer.Deserialize<object[]>(ref reader, ErrorSerializerContext.Default.ObjectArray); // TODO: Test! This might not work without adding `object[]` to `ErrorSerializationContext`
			var arrayDict = new Dictionary<string, object>();
			for (var i = 0; i < array.Length; i++)
				arrayDict[i.ToString(CultureInfo.InvariantCulture)] = new DynamicValue(array[i]);
			return DynamicDictionary.Create(arrayDict);
		}
		if (reader.TokenType != JsonTokenType.StartObject) throw new JsonException();

		var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(ref reader, ErrorSerializerContext.Default.DictionaryStringObject); // TODO: Test! This might not work without adding `Dictionary<string, object>` to `ErrorSerializationContext`
		return DynamicDictionary.Create(dict);
	}

	public override void Write(Utf8JsonWriter writer, DynamicDictionary dictionary, JsonSerializerOptions options)
	{
		writer.WriteStartObject();

		foreach (var kvp in dictionary)
		{
			if (kvp.Value is null) continue;

			writer.WritePropertyName(kvp.Key);

			// TODO: Test! We have to make sure all possible "Value" types are registered in the `ErrorSerializationContext`
#pragma warning disable IL2026, IL3050 // ErrorSerializerContext is registered.
			JsonSerializer.Serialize(writer, kvp.Value.Value, kvp.Value.GetType(), options);
#pragma warning restore IL2026, IL3050
		}

		writer.WriteEndObject();
	}
}
