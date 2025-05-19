// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Elastic.Transport;

/// <summary>
/// A Json Converter that serializes an exception by flattening it and
/// inner exceptions into an array of objects, including depth.
/// </summary>
internal class ExceptionConverter : JsonConverter<Exception>
{
	public override bool CanConvert(Type typeToConvert) => typeof(Exception).IsAssignableFrom(typeToConvert);

	private static List<Dictionary<string, object>> FlattenExceptions(Exception e)
	{
		var maxExceptions = 20;
		var exceptions = new List<Dictionary<string, object>>(maxExceptions);
		var depth = 0;
		do
		{
			var o = ToDictionary(e, depth);
			exceptions.Add(o);
			depth++;
			e = e.InnerException;
		} while (depth < maxExceptions && e != null);

		return exceptions;
	}

	private static Dictionary<string, object> ToDictionary(Exception e, int depth)
	{
		var o = new Dictionary<string, object>(7);

		var className = e.GetType().FullName;

		o.Add("Depth", depth);
		o.Add("ClassName", className);
		o.Add("Message", e.Message);
		o.Add("Source", e.Source);
		o.Add("StackTraceString", e.StackTrace);
		o.Add("HResult", e.HResult);
		o.Add("HelpURL", e.HelpLink);

		return o;
	}

	public override Exception Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
		throw new NotSupportedException();

	public override void Write(Utf8JsonWriter writer, Exception value, JsonSerializerOptions options)
	{
		if (value == null)
		{
			writer.WriteNullValue();
			return;
		}

		var flattenedExceptions = FlattenExceptions(value);
		writer.WriteStartArray();
		for (var i = 0; i < flattenedExceptions.Count; i++)
		{
			var flattenedException = flattenedExceptions[i];
			writer.WriteStartObject();
			foreach (var kv in flattenedException)
			{
				writer.WritePropertyName(kv.Key);
				JsonSerializer.Serialize(writer, kv.Value, options); // TODO: Test! This might not work without adding `KeyValuePair<string, object>` to `ErrorSerializationContext`
			}
			writer.WriteEndObject();
		}
		writer.WriteEndArray();
	}
}
