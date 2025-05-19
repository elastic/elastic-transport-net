// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Elastic.Transport.Products.Elasticsearch;

/// <summary> Represents the serialized Elasticsearch java exception that caused a request to fail </summary>
[DataContract]
[JsonConverter(typeof(ErrorConverter))]
public sealed class Error : ErrorCause
{
	private static readonly IReadOnlyDictionary<string, string> DefaultHeaders =
		new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(0));

	/// <summary> Additional headers from the request that pertain to the error</summary>
	[JsonPropertyName("headers")]
	public IReadOnlyDictionary<string, string> Headers { get; set; } = DefaultHeaders;

	/// <summary> The root cause exception </summary>
	[JsonPropertyName("root_cause")]
	public IReadOnlyCollection<ErrorCause> RootCause { get; set; }

	/// <summary> A human readable string representation of the exception returned by Elasticsearch </summary>
	public override string ToString() => CausedBy == null
		? $"Type: {Type} Reason: \"{Reason}\""
		: $"Type: {Type} Reason: \"{Reason}\" CausedBy: \"{CausedBy}\"";
}

/// A JSON converter for <see cref="Error"/>.
public sealed class ErrorConverter : JsonConverter<Error>
{
	/// <inheritdoc cref="JsonConverter{T}.Read"/>
	public override Error Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		if (reader.TokenType == JsonTokenType.String)
			return new Error { Reason = reader.GetString() };
		if (reader.TokenType == JsonTokenType.StartObject)
		{
			var error = new Error();
			Dictionary<string, object> additional = null;

			while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
			{
				if (reader.TokenType != JsonTokenType.PropertyName) continue;

				if (reader.ValueTextEquals("root_cause"))
				{
					var value = JsonSerializer.Deserialize<IReadOnlyCollection<ErrorCause>>(ref reader, ErrorSerializerContext.Default.IReadOnlyCollectionErrorCause);
					error.RootCause = value;
					continue;
				}

				if (reader.ValueTextEquals("caused_by"))
				{
					var value = JsonSerializer.Deserialize<ErrorCause>(ref reader, ErrorSerializerContext.Default.ErrorCause);
					error.CausedBy = value;
					continue;
				}

				//if (reader.ValueTextEquals("suppressed"))
				//{
				//	var value = JsonSerializer.Deserialize<IReadOnlyCollection<ErrorCause>>(ref reader, options);
				//	error.TODO = value;
				//	continue;
				//}

				if (reader.ValueTextEquals("headers"))
				{
					var value = JsonSerializer.Deserialize<IReadOnlyDictionary<string, string>>(ref reader, ErrorSerializerContext.Default.IReadOnlyDictionaryStringString); // TODO: Test! This might not work without adding `IReadOnlyDictionary<string, string>` to `ErrorSerializationContext`
					error.Headers = value;
					continue;
				}

				if (reader.ValueTextEquals("stack_trace"))
				{
					var value = JsonSerializer.Deserialize<string>(ref reader, ErrorSerializerContext.Default.String);
					error.StackTrace = value;
					continue;
				}

				if (reader.ValueTextEquals("type"))
				{
					var value = JsonSerializer.Deserialize<string>(ref reader, ErrorSerializerContext.Default.String);
					error.Type = value;
					continue;
				}

				if (reader.ValueTextEquals("index"))
				{
					var value = JsonSerializer.Deserialize<string>(ref reader, ErrorSerializerContext.Default.String);
					error.Index = value;
					continue;
				}

				if (reader.ValueTextEquals("index_uuid"))
				{
					var value = JsonSerializer.Deserialize<string>(ref reader, ErrorSerializerContext.Default.String);
					error.IndexUUID = value;
					continue;
				}

				if (reader.ValueTextEquals("reason"))
				{
					var value = JsonSerializer.Deserialize<string>(ref reader, ErrorSerializerContext.Default.String);
					error.Reason = value;
					continue;
				}

				additional ??= new Dictionary<string, object>();
				var key = reader.GetString();
				var additionaValue = JsonSerializer.Deserialize<object>(ref reader, ErrorSerializerContext.Default.Object);
				additional.Add(key, additionaValue);
			}

			if (additional is not null)
				error.AdditionalProperties = additional;

			return error;
		}

		throw new JsonException("Could not deserialise the error response.");
	}

	/// <inheritdoc cref="JsonConverter{T}.Read"/>
	public override void Write(Utf8JsonWriter writer, Error value, JsonSerializerOptions options) =>
		throw new NotImplementedException();
}
