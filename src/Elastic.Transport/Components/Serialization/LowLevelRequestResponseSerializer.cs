// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Elastic.Transport.Extensions;
using static Elastic.Transport.SerializationFormatting;

namespace Elastic.Transport;

/// <summary>
/// Default implementation for <see cref="Serializer"/>. This uses <see cref="JsonSerializer"/> from <code>System.Text.Json</code>.
/// </summary>
internal sealed class LowLevelRequestResponseSerializer : Serializer
{
	/// <summary>
	/// Provides a static reusable reference to an instance of <see cref="LowLevelRequestResponseSerializer"/> to promote reuse.
	/// </summary>
	internal static readonly LowLevelRequestResponseSerializer Instance = new();

	private readonly Lazy<JsonSerializerOptions> _indented;
	private readonly Lazy<JsonSerializerOptions> _none;

	private IReadOnlyCollection<JsonConverter> AdditionalConverters { get; }

	private IList<JsonConverter> BakedInConverters { get; } = new List<JsonConverter>
		{
			{ new ExceptionConverter() },
			{ new ErrorCauseConverter() },
			{ new ErrorConverter() },
			{ new DynamicDictionaryConverter() }
		};

	/// <inheritdoc cref="LowLevelRequestResponseSerializer"/>>
	public LowLevelRequestResponseSerializer() : this(null) { }

	/// <summary>
	/// <inheritdoc cref="LowLevelRequestResponseSerializer"/>>
	/// </summary>
	/// <param name="converters">Add more default converters onto <see cref="JsonSerializerOptions"/> being used</param>
	public LowLevelRequestResponseSerializer(IEnumerable<JsonConverter> converters)
	{
		AdditionalConverters = converters != null
			? new ReadOnlyCollection<JsonConverter>(converters.ToList())
			: EmptyReadOnly<JsonConverter>.Collection;
		_indented = new Lazy<JsonSerializerOptions>(() => CreateSerializerOptions(Indented));
		_none = new Lazy<JsonSerializerOptions>(() => CreateSerializerOptions(None));
	}

	/// <summary>
	/// Creates <see cref="JsonSerializerOptions"/> used for serialization.
	/// Override on a derived serializer to change serialization.
	/// </summary>
	public JsonSerializerOptions CreateSerializerOptions(SerializationFormatting formatting)
	{
		var options = new JsonSerializerOptions
		{
			DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
			WriteIndented = formatting == Indented,
		};
		foreach (var converter in BakedInConverters)
			options.Converters.Add(converter);
		foreach (var converter in AdditionalConverters)
			options.Converters.Add(converter);

		return options;

	}

	private static bool TryReturnDefault<T>(Stream stream, out T deserialize)
	{
		deserialize = default;
		return stream == null || stream == Stream.Null || (stream.CanSeek && stream.Length == 0);
	}

	private JsonSerializerOptions GetFormatting(SerializationFormatting formatting) => formatting == None ? _none.Value : _indented.Value;

	/// <inheritdoc cref="Serializer.Deserialize"/>>
	public override object Deserialize(Type type, Stream stream)
	{
		if (TryReturnDefault(stream, out object deserialize)) return deserialize;

		return JsonSerializer.Deserialize(stream, type, _none.Value);
	}

	/// <inheritdoc cref="Serializer.Deserialize{T}"/>>
	public override T Deserialize<T>(Stream stream)
	{
		if (TryReturnDefault(stream, out T deserialize)) return deserialize;

		return JsonSerializer.Deserialize<T>(stream, _none.Value);
	}

	/// <inheritdoc cref="Serializer.Serialize{T}"/>>
	public override void Serialize<T>(T data, Stream stream, SerializationFormatting formatting = None)
	{
		using var writer = new Utf8JsonWriter(stream);
		if (data == null)
			JsonSerializer.Serialize(writer, null, typeof(object), GetFormatting(formatting));
		//TODO validate if we can avoid boxing by checking if data is typeof(object)
		else
			JsonSerializer.Serialize(writer, data, data.GetType(), GetFormatting(formatting));
	}

	/// <inheritdoc cref="Serializer.SerializeAsync{T}"/>>
	public override async Task SerializeAsync<T>(T data, Stream stream, SerializationFormatting formatting = None,
		CancellationToken cancellationToken = default
	)
	{
		if (data == null)
			await JsonSerializer.SerializeAsync(stream, null, typeof(object), GetFormatting(formatting), cancellationToken).ConfigureAwait(false);
		else
			await JsonSerializer.SerializeAsync(stream, data, data.GetType(), GetFormatting(formatting), cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc cref="Serializer.DeserializeAsync"/>>
	public override ValueTask<object> DeserializeAsync(Type type, Stream stream, CancellationToken cancellationToken = default)
	{
		if (TryReturnDefault(stream, out object deserialize)) return new ValueTask<object>(deserialize);

		return JsonSerializer.DeserializeAsync(stream, type, _none.Value, cancellationToken);
	}

	/// <inheritdoc cref="Serializer.DeserializeAsync{T}"/>>
	public override ValueTask<T> DeserializeAsync<T>(Stream stream, CancellationToken cancellationToken = default)
	{
		if (TryReturnDefault(stream, out T deserialize)) return new ValueTask<T>(deserialize);

		return JsonSerializer.DeserializeAsync<T>(stream, _none.Value, cancellationToken);
	}
}
