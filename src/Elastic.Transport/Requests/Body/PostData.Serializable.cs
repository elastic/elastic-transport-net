// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.IO;
using System.Threading;
using System.Threading.Tasks;
using static Elastic.Transport.SerializationFormatting;

namespace Elastic.Transport;

public abstract partial class PostData
{
	/// <summary>
	/// Create a <see cref="PostData"/> instance that will serialize <paramref name="data"/> using
	/// <see cref="Serializer"/>
	/// </summary>
	public static PostData Serializable<T>(T data) => new SerializableData<T>(data);

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
}
