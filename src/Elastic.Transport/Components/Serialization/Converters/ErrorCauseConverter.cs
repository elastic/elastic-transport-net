// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using Elastic.Transport.Products.Elasticsearch;

namespace Elastic.Transport;

/// A JSON converter for <see cref="ErrorCause"/>
public class ErrorCauseConverter : ErrorCauseConverter<ErrorCause> { }

/// A JSON converter for <see cref="Error"/>
public class ErrorConverter : ErrorCauseConverter<Error>
{
	/// <inheritdoc cref="ErrorCauseConverter{T}.ReadMore"/>
	[UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026:RequiresUnreferencedCode", Justification = "We always provide a static JsonTypeInfoResolver")]
	[UnconditionalSuppressMessage("AotAnalysis", "IL3050:RequiresDynamicCode", Justification = "We always provide a static JsonTypeInfoResolver")]
	protected override bool ReadMore(ref Utf8JsonReader reader, JsonSerializerOptions options, string propertyName, Error errorCause)
	{
		void ReadAssign<T>(ref Utf8JsonReader r, Action<Error, T?> set) =>
			set(errorCause, JsonSerializer.Deserialize<T>(ref r, options));
		switch (propertyName)
		{
			case "headers":
				reader.Read();
				var headers = JsonSerializer.Deserialize<Dictionary<string, string>>(ref reader, options);
				if (headers != null)
					errorCause.Headers = headers;
				return true;

			case "root_cause":
				ReadAssign<IReadOnlyCollection<ErrorCause>?>(ref reader, (e, v) => e.RootCause = v);
				return true;
			default:
				return false;

		}
	}
}

/// A JSON converter for <see cref="ErrorCause"/> implementations
public abstract class ErrorCauseConverter<TErrorCause> : JsonConverter<TErrorCause> where TErrorCause : ErrorCause, new()
{
	/// <inheritdoc cref="JsonConverter{T}.Read"/>
	[UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026:RequiresUnreferencedCode", Justification = "We always provide a static JsonTypeInfoResolver")]
	[UnconditionalSuppressMessage("AotAnalysis", "IL3050:RequiresDynamicCode", Justification = "We always provide a static JsonTypeInfoResolver")]
	public override TErrorCause? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		if (reader.TokenType != JsonTokenType.StartObject)
		{
			return reader.TokenType == JsonTokenType.String
				? new TErrorCause { Reason = reader.GetString() }
				: null;
		}

		var errorCause = new TErrorCause();
		var additionalProperties = new Dictionary<string, object>();
		errorCause.AdditionalProperties = additionalProperties;

		void ReadAssign<T>(ref Utf8JsonReader r, Action<ErrorCause, T?> set) =>
			set(errorCause, JsonSerializer.Deserialize<T>(ref r, options));

		void ReadAny(ref Utf8JsonReader r, string property, Action<ErrorCause, string, object> set) =>
			set(errorCause, property, JsonSerializer.Deserialize<JsonElement>(ref r, options));

		while (reader.Read())
		{
			if (reader.TokenType == JsonTokenType.EndObject) return errorCause;

			if (reader.TokenType != JsonTokenType.PropertyName) throw new JsonException();

			var propertyName = reader.GetString();
			switch (propertyName)
			{
				//case "bytes_limit":
				//	ReadAssign<int?>(ref reader, (e, v) => e.BytesLimit = v);
				//	break;
				//case "bytes_wanted":
				//	ReadAssign<int?>(ref reader, (e, v) => e.BytesWanted = v);
				//	break;
				case "caused_by":
					ReadAssign<ErrorCause>(ref reader, (e, v) => e.CausedBy = v);
					break;
				//case "col":
				//	ReadAssign<int?>(ref reader, (e, v) => e.Column = v);
				//	break;
				//case "failed_shards":
				//	ReadAssign<IReadOnlyCollection<ShardFailure>>(ref reader, (e, v) => e.FailedShards = v);
				//	break;
				//case "grouped":
				//	ReadAssign<bool?>(ref reader, (e, v) => e.Grouped = v);
				//	break;
				case "index":
					ReadAssign<string>(ref reader, (e, v) => e.Index = v);
					break;
				case "index_uuid":
					ReadAssign<string>(ref reader, (e, v) => e.IndexUUID = v);
					break;
				//case "lang":
				//	ReadAssign<string>(ref reader, (e, v) => e.Language = v);
				//	break;
				//case "license.expired.feature":
				//	ReadAssign<string>(ref reader, (e, v) => e.LicensedExpiredFeature = v);
				//	break;
				//case "line":
				//	ReadAssign<int?>(ref reader, (e, v) => e.Line = v);
				//	break;
				//case "phase":
				//	ReadAssign<string>(ref reader, (e, v) => e.Phase = v);
				//	break;
				case "reason":
					ReadAssign<string>(ref reader, (e, v) => e.Reason = v);
					break;
				//case "resource.id":
				//	errorCause.ResourceId = ReadSingleOrCollection(ref reader, options);
				//	break;
				//case "resource.type":
				//	ReadAssign<string>(ref reader, (e, v) => e.ResourceType = v);
				//	break;
				//case "script":
				//	ReadAssign<string>(ref reader, (e, v) => e.Script = v);
				//	break;
				//case "script_stack":
				//	errorCause.ScriptStack = ReadSingleOrCollection(ref reader, options);
				//	break;
				//case "shard":
				//	errorCause.Shard = ReadIntFromString(ref reader, options);
				//	break;
				case "stack_trace":
					ReadAssign<string>(ref reader, (e, v) => e.StackTrace = v);
					break;
				case "type":
					ReadAssign<string>(ref reader, (e, v) => e.Type = v);
					break;
				default:
					if (ReadMore(ref reader, options, propertyName!, errorCause)) break;
					else
					{
						ReadAny(ref reader, propertyName!, (_, p, v) => additionalProperties.Add(p, v));
						break;
					}
			}
		}
		return errorCause;
	}

	/// Read additional properties for the particular <see cref="ErrorCause"/> implementation
	protected virtual bool ReadMore(ref Utf8JsonReader reader, JsonSerializerOptions options, string propertyName, TErrorCause errorCause) => false;

	/// <inheritdoc cref="JsonConverter{T}.Read"/>
	[UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026:RequiresUnreferencedCode", Justification = "We always provide a static JsonTypeInfoResolver")]
	[UnconditionalSuppressMessage("AotAnalysis", "IL3050:RequiresDynamicCode", Justification = "We always provide a static JsonTypeInfoResolver")]
	public override void Write(Utf8JsonWriter writer, TErrorCause value, JsonSerializerOptions options)
	{
		writer.WriteStartObject();

		static void Serialize<T>(Utf8JsonWriter writer, JsonSerializerOptions options, string name, T value)
		{
			if (value is null) return;

			writer.WritePropertyName(name);
			JsonSerializer.Serialize(writer, value, options);
		}

		static void SerializeDynamic(Utf8JsonWriter writer, JsonSerializerOptions options, string name, object? value, Type inputType)
		{
			if (value is null) return;

			writer.WritePropertyName(name);
			JsonSerializer.Serialize(writer, value, inputType, options);
		}

		//Serialize(writer, options, "bytes_limit", value.BytesLimit);
		//Serialize(writer, options, "bytes_wanted", value.BytesWanted);
		Serialize(writer, options, "caused_by", value.CausedBy);
		//Serialize(writer, options, "col", value.Column);
		//Serialize(writer, options, "failed_shards", value.FailedShards);
		//Serialize(writer, options, "grouped", value.Grouped);
		Serialize(writer, options, "index", value.Index);
		Serialize(writer, options, "index_uuid", value.IndexUUID);
		//Serialize(writer, options, "lang", value.Language);
		//Serialize(writer, options, "license.expired.feature", value.LicensedExpiredFeature);
		//Serialize(writer, options, "line", value.Line);
		//Serialize(writer, options, "phase", value.Phase);
		//Serialize(writer, options, "reason", value.Reason);
		//Serialize(writer, options, "resource.id", value.ResourceId);
		//Serialize(writer, options, "resource.type", value.ResourceType);
		//Serialize(writer, options, "script", value.Script);
		//Serialize(writer, options, "script_stack", value.ScriptStack);
		//Serialize(writer, options, "shard", value.Shard);
		Serialize(writer, options, "stack_trace", value.StackTrace);
		Serialize(writer, options, "type", value.Type);

		foreach (var kv in value.AdditionalProperties)
			SerializeDynamic(writer, options, kv.Key, kv.Value, kv.Value.GetType());
		writer.WriteEndObject();
	}
}
