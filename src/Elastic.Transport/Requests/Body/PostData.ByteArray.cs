// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Elastic.Transport;

public abstract partial class PostData
{
	/// <summary>
	/// Create a <see cref="PostData"/> instance that will write <paramref name="bytes"/> to the output <see cref="Stream"/>
	/// </summary>
	// ReSharper disable once MemberCanBePrivate.Global
	public static PostData Bytes(byte[] bytes) => new PostDataByteArray(bytes);

	private class PostDataByteArray : PostData
	{
		protected internal PostDataByteArray(byte[] item)
		{
			WrittenBytes = item;
			Type = PostType.ByteArray;
		}

		public override void Write(Stream writableStream, ITransportConfiguration settings)
		{
			if (WrittenBytes == null) return;

			var stream = InitWrite(writableStream, settings, out var buffer, out var disableDirectStreaming);

			if (!disableDirectStreaming)
				stream.Write(WrittenBytes, 0, WrittenBytes.Length);
			else
				buffer = settings.MemoryStreamFactory.Create(WrittenBytes);

			FinishStream(writableStream, buffer, settings);
		}

		public override async Task WriteAsync(Stream writableStream, ITransportConfiguration settings,
			CancellationToken cancellationToken)
		{
			if (WrittenBytes == null) return;

			var stream = InitWrite(writableStream, settings, out var buffer, out var disableDirectStreaming);

			if (!disableDirectStreaming)
				await stream.WriteAsync(WrittenBytes, 0, WrittenBytes.Length, cancellationToken)
					.ConfigureAwait(false);
			else
				buffer = settings.MemoryStreamFactory.Create(WrittenBytes);

			await FinishStreamAsync(writableStream, buffer, settings, cancellationToken).ConfigureAwait(false);
		}
	}
}
