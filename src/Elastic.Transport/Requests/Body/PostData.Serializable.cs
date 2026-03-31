// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;
using static Elastic.Transport.SerializationFormatting;

namespace Elastic.Transport;

public abstract partial class PostData
{
	/// <summary>
	/// Create a <see cref="PostData"/> instance that will serialize <paramref name="data"/> using
	/// <see cref="Serializer"/>.
	/// </summary>
	/// <remarks>
	/// In AOT/trimmed applications, <typeparamref name="T"/> must be registered in the configured
	/// <c>JsonSerializerContext</c>; otherwise serialization will throw at runtime.
	/// Prefer the <see cref="Serializable{T}(T, JsonTypeInfo{T})"/> overload for guaranteed AOT safety.
	/// </remarks>
	public static PostData Serializable<T>(T data) => new SerializableData<T>(data);

	/// <summary>
	/// Create a <see cref="PostData"/> instance that will serialize <paramref name="data"/> using the
	/// provided <paramref name="typeInfo"/>. This overload is AOT-safe and does not require reflection.
	/// </summary>
	public static PostData Serializable<T>(T data, JsonTypeInfo<T> typeInfo) =>
		new TypeInfoSerializableData<T>(data, typeInfo);

	private class SerializableData<T> : PostData
	{
		private readonly T _serializable;

		public SerializableData(T item)
		{
			Type = PostType.Serializable;
			_serializable = item;
		}

		public static implicit operator SerializableData<T>(T serializableData) => new(serializableData);

		public override void Write(Stream writableStream, ITransportConfiguration settings, bool disableDirectStreaming)
		{
			MemoryStream? buffer = null;
			var stream = writableStream;
			BufferIfNeeded(settings.MemoryStreamFactory, disableDirectStreaming, ref buffer, ref stream);

			var indent = settings.PrettyJson ? Indented : None;
			settings.RequestResponseSerializer.Serialize(_serializable, stream, indent);

			FinishStream(writableStream, buffer, disableDirectStreaming);
		}

		public override async Task WriteAsync(Stream writableStream, ITransportConfiguration settings, bool disableDirectStreaming, CancellationToken cancellationToken)
		{
			MemoryStream? buffer = null;
			var stream = writableStream;
			BufferIfNeeded(settings.MemoryStreamFactory, disableDirectStreaming, ref buffer, ref stream);

			var indent = settings.PrettyJson ? Indented : None;
			await settings.RequestResponseSerializer
				.SerializeAsync(_serializable, stream, indent, cancellationToken)
				.ConfigureAwait(false);

			await FinishStreamAsync(writableStream, buffer, disableDirectStreaming, cancellationToken).ConfigureAwait(false);
		}
	}

	private class TypeInfoSerializableData<T> : PostData
	{
		private readonly T _serializable;
		private readonly JsonTypeInfo<T> _typeInfo;

		public TypeInfoSerializableData(T item, JsonTypeInfo<T> typeInfo)
		{
			Type = PostType.Serializable;
			_serializable = item;
			_typeInfo = typeInfo;
		}

		public override void Write(Stream writableStream, ITransportConfiguration settings, bool disableDirectStreaming)
		{
			MemoryStream? buffer = null;
			var stream = writableStream;
			BufferIfNeeded(settings.MemoryStreamFactory, disableDirectStreaming, ref buffer, ref stream);

			JsonSerializer.Serialize(stream, _serializable, _typeInfo);

			FinishStream(writableStream, buffer, disableDirectStreaming);
		}

		public override async Task WriteAsync(Stream writableStream, ITransportConfiguration settings, bool disableDirectStreaming, CancellationToken cancellationToken)
		{
			MemoryStream? buffer = null;
			var stream = writableStream;
			BufferIfNeeded(settings.MemoryStreamFactory, disableDirectStreaming, ref buffer, ref stream);

			await JsonSerializer.SerializeAsync(stream, _serializable, _typeInfo, cancellationToken)
				.ConfigureAwait(false);

			await FinishStreamAsync(writableStream, buffer, disableDirectStreaming, cancellationToken).ConfigureAwait(false);
		}
	}
}
