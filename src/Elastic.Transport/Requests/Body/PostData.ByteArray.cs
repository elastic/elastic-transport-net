// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
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

		public override void Write(Stream writableStream, ITransportConfiguration settings, bool disableDirectStreaming)
		{
			if (WrittenBytes == null)
				return;

			MemoryStream? buffer = null;

			if (!disableDirectStreaming)
				writableStream.Write(WrittenBytes, 0, WrittenBytes.Length);
			else
				buffer = settings.MemoryStreamFactory.Create(WrittenBytes);

			FinishStream(writableStream, buffer, disableDirectStreaming);
		}

		public override async Task WriteAsync(Stream writableStream, ITransportConfiguration settings, bool disableDirectStreaming, CancellationToken cancellationToken)
		{
			if (WrittenBytes == null)
				return;

			MemoryStream? buffer = null;

			if (!disableDirectStreaming)
#if NETSTANDARD2_1_OR_GREATER || NET
				await writableStream.WriteAsync(WrittenBytes.AsMemory(), cancellationToken)
					.ConfigureAwait(false);
#else
				await writableStream.WriteAsync(WrittenBytes, 0, WrittenBytes.Length, cancellationToken)
					.ConfigureAwait(false);
#endif
			else
				buffer = settings.MemoryStreamFactory.Create(WrittenBytes);

			await FinishStreamAsync(writableStream, buffer, disableDirectStreaming, cancellationToken).ConfigureAwait(false);
		}
	}
}
