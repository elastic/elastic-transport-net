// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Elastic.Transport;

/// <summary>
/// An abstract implementation of a transport <see cref="Serializer"/> which serializes using the Microsoft
/// <c>System.Text.Json</c> library.
/// </summary>
public abstract class SystemTextJsonSerializer :
	Serializer
{
	private readonly SemaphoreSlim _semaphore = new(1);

	private bool _initialized;
	private JsonSerializerOptions? _options;
	private JsonSerializerOptions? _indentedOptions;

	#region Serializer

	/// <inheritdoc />
	public override T Deserialize<T>(Stream stream)
	{
		Initialize();

		if (TryReturnDefault(stream, out T deserialize))
			return deserialize;

		return JsonSerializer.Deserialize<T>(stream, _options);
	}

	/// <inheritdoc />
	public override object? Deserialize(Type type, Stream stream)
	{
		Initialize();

		if (TryReturnDefault(stream, out object deserialize))
			return deserialize;

		return JsonSerializer.Deserialize(stream, type, _options);
	}

	/// <inheritdoc />
	public override ValueTask<T> DeserializeAsync<T>(Stream stream, CancellationToken cancellationToken = default)
	{
		Initialize();

		if (TryReturnDefault(stream, out T deserialize))
			return new ValueTask<T>(deserialize);

		return JsonSerializer.DeserializeAsync<T>(stream, _options, cancellationToken);
	}

	/// <inheritdoc />
	public override ValueTask<object?> DeserializeAsync(Type type, Stream stream, CancellationToken cancellationToken = default)
	{
		Initialize();

		if (TryReturnDefault(stream, out object deserialize))
			return new ValueTask<object?>(deserialize);

		return JsonSerializer.DeserializeAsync(stream, type, _options, cancellationToken);
	}

	/// <inheritdoc />
	public override void Serialize<T>(T data, Stream writableStream,
		SerializationFormatting formatting = SerializationFormatting.None)
	{
		Initialize();

		JsonSerializer.Serialize(writableStream, data, GetJsonSerializerOptions(formatting));
	}

	/// <inheritdoc />
	public override Task SerializeAsync<T>(T data, Stream stream,
		SerializationFormatting formatting = SerializationFormatting.None,
		CancellationToken cancellationToken = default)
	{
		Initialize();

		return JsonSerializer.SerializeAsync(stream, data, GetJsonSerializerOptions(formatting), cancellationToken);
	}

	#endregion Serializer

	/// <summary>
	/// A factory method that can create an instance of <see cref="JsonSerializerOptions"/> that will
	/// be used when serializing.
	/// </summary>
	/// <returns></returns>
	protected abstract JsonSerializerOptions? CreateJsonSerializerOptions();

	/// <summary>
	/// A callback function that is invoked after the <see cref="JsonSerializerOptions"/> have been created and the
	/// serializer got fully initialized.
	/// </summary>
	protected virtual void Initialized()
	{
	}

	/// <summary>
	/// Returns the <see cref="JsonSerializerOptions"/> for this serializer, based on the given <paramref name="formatting"/>.
	/// </summary>
	/// <param name="formatting">The serialization formatting.</param>
	/// <returns>The requested <see cref="JsonSerializerOptions"/> or <c>null</c>, if the serializer is not initialized yet.</returns>
	protected internal JsonSerializerOptions? GetJsonSerializerOptions(SerializationFormatting formatting = SerializationFormatting.None) =>
		(formatting is SerializationFormatting.None)
			? _options
			: _indentedOptions;

	/// <summary>
	/// Initializes a serializer instance such that its <see cref="JsonSerializerOptions"/> are populated.
	/// </summary>
	protected internal void Initialize()
	{
		// Exit early, if already initialized
		if (_initialized)
			return;

		_semaphore.Wait();

		try
		{
			// Exit early, if the current thread lost the race
			if (_initialized)
				return;

			var options = CreateJsonSerializerOptions();

			if (options is null)
			{
				_options = new JsonSerializerOptions();
				_indentedOptions = new JsonSerializerOptions
				{
					WriteIndented = true
				};
			}
			else
			{
				_options = options;
				_indentedOptions = new JsonSerializerOptions(options)
				{
					WriteIndented = true
				};
			}

			_initialized = true;

			Initialized();
		}
		finally
		{
			_semaphore.Release();
		}
	}

	private static bool TryReturnDefault<T>(Stream? stream, out T deserialize)
	{
		deserialize = default;
		return (stream is null) || stream == Stream.Null || (stream.CanSeek && stream.Length == 0);
	}
}
