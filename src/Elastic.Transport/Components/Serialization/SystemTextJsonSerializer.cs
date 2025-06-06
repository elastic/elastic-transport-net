// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Elastic.Transport;

#pragma warning disable IL2026, IL3050 // Implementing classes must make sure to use an AOT compatible JsonSerializerOptions.TypeInfoResolver

/// <summary>
/// An abstract implementation of a transport <see cref="Serializer"/> which serializes using the Microsoft
/// <c>System.Text.Json</c> library.
/// </summary>
public abstract class SystemTextJsonSerializer : Serializer
{
	private readonly JsonSerializerOptions _options;
	private readonly JsonSerializerOptions _indentedOptions;

	/// <summary>
	/// An abstract implementation of a transport <see cref="Serializer"/> which serializes using the Microsoft
	/// <c>System.Text.Json</c> library.
	/// </summary>
	protected SystemTextJsonSerializer(IJsonSerializerOptionsProvider? provider = null)
	{
		provider ??= new TransportSerializerOptionsProvider();
		_options = provider.CreateJsonSerializerOptions();
		_indentedOptions = new JsonSerializerOptions(_options)
		{
			WriteIndented = true
		};
	}

	#region Serializer

	/// <inheritdoc />
	[UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026:RequiresUnreferencedCode", Justification = "We always provide a static JsonTypeInfoResolver")]
	[UnconditionalSuppressMessage("AotAnalysis", "IL3050:RequiresDynamicCode", Justification = "We always provide a static JsonTypeInfoResolver")]
	public override T Deserialize<T>(Stream stream)
	{
		if (TryReturnDefault(stream, out T deserialize))
			return deserialize;

		return JsonSerializer.Deserialize<T>(stream, GetJsonSerializerOptions());
	}

	/// <inheritdoc />
	public override object? Deserialize(Type type, Stream stream)
	{
		if (TryReturnDefault(stream, out object deserialize))
			return deserialize;

		return JsonSerializer.Deserialize(stream, type, GetJsonSerializerOptions());
	}

	/// <inheritdoc />
	[UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026:RequiresUnreferencedCode", Justification = "We always provide a static JsonTypeInfoResolver")]
	[UnconditionalSuppressMessage("AotAnalysis", "IL3050:RequiresDynamicCode", Justification = "We always provide a static JsonTypeInfoResolver")]
	public override ValueTask<T> DeserializeAsync<T>(Stream stream, CancellationToken cancellationToken = default)
	{
		if (TryReturnDefault(stream, out T deserialize))
			return new ValueTask<T>(deserialize);

		return JsonSerializer.DeserializeAsync<T>(stream, GetJsonSerializerOptions(), cancellationToken);
	}

	/// <inheritdoc />
	public override ValueTask<object?> DeserializeAsync(Type type, Stream stream, CancellationToken cancellationToken = default)
	{
		if (TryReturnDefault(stream, out object deserialize))
			return new ValueTask<object?>(deserialize);

		return JsonSerializer.DeserializeAsync(stream, type, GetJsonSerializerOptions(), cancellationToken);
	}

	/// <inheritdoc />
	public override void Serialize<T>(T data, Stream stream,
		SerializationFormatting formatting = SerializationFormatting.None) =>
		JsonSerializer.Serialize(stream, data, GetJsonSerializerOptions(formatting));

	/// <inheritdoc />
	public override void Serialize(object? data, Type type, Stream stream, SerializationFormatting formatting = SerializationFormatting.None,
		CancellationToken cancellationToken = default) =>
		JsonSerializer.Serialize(stream, data, type, GetJsonSerializerOptions(formatting));

	/// <inheritdoc />
	public override Task SerializeAsync<T>(T data, Stream stream,
		SerializationFormatting formatting = SerializationFormatting.None,
		CancellationToken cancellationToken = default) =>
		JsonSerializer.SerializeAsync(stream, data, GetJsonSerializerOptions(formatting), cancellationToken);

	/// <inheritdoc />
	public override Task SerializeAsync(object? data, Type type, Stream stream, SerializationFormatting formatting = SerializationFormatting.None,
		CancellationToken cancellationToken = default) =>
		JsonSerializer.SerializeAsync(stream, data, type, GetJsonSerializerOptions(formatting), cancellationToken);

	#endregion Serializer

	/// <summary>
	/// Override to (dis-)allow fast-path (de-)serialization for specific types.
	/// </summary>
	/// <param name="type">The <see cref="Type"/> that is being (de-)serialized.</param>
	/// <returns>
	/// <see langword="true"/> if the given <paramref name="type"/> supports fast-path (de-)serialization or
	/// <see langword="false"/>, if not.
	/// </returns>
	/// <remarks>
	/// <para>
	///	Most extension methods in <see cref="Extensions.TransportSerializerExtensions"/> will prefer fast-path (de-)serialization, when
	/// used with <see cref="SystemTextJsonSerializer"/> based serializer implementations.
	/// Fast-path (de-)serialization bypasses the <see cref="Deserialize"/>, <see cref="Deserialize{T}"/>, <see cref="Serialize{T}"/>
	/// methods and directly uses the <see cref="JsonSerializer"/> API instead.
	/// </para>
	/// <para>
	///	In some cases, when the concrete <see cref="SystemTextJsonSerializer"/> based serializer implementation overrides one or more of
	/// the previously named methods, the default fast-path behavior is probably undesired as it would prevent the user defined code in
	/// the overwritten methods from being executed.
	/// The <see cref="SupportsFastPath"/> method can be used to either completely disable fast-path (de-)serialization or to selectively
	/// allow it for specific types only.
	/// </para>
	/// </remarks>
	protected internal virtual bool SupportsFastPath(Type type) => true;

	/// <summary>
	/// Returns the <see cref="JsonSerializerOptions"/> for this serializer, based on the given <paramref name="formatting"/>.
	/// </summary>
	/// <param name="formatting">The serialization formatting.</param>
	/// <returns>The requested <see cref="JsonSerializerOptions"/>.</returns>
	protected internal JsonSerializerOptions GetJsonSerializerOptions(SerializationFormatting formatting = SerializationFormatting.None) =>
		formatting is SerializationFormatting.None ? _options : _indentedOptions;

	private static bool TryReturnDefault<T>(Stream? stream, out T deserialize)
	{
		deserialize = default;
		return (stream is null) || stream == Stream.Null || (stream.CanSeek && stream.Length == 0);
	}
}

#pragma warning restore IL2026, IL3050
