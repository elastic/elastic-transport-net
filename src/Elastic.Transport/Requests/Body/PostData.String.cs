// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Elastic.Transport.Extensions;

namespace Elastic.Transport;

public abstract partial class PostData
{
	/// <summary>
	/// Create a <see cref="PostData"/> instance that will write <paramref name="serializedString"/> to the output <see cref="Stream"/>
	/// </summary>
	// ReSharper disable once MemberCanBePrivate.Global
	public static PostData String(string serializedString) => new PostDataString(serializedString);

	/// <summary>
	/// string implicitly converts to <see cref="PostData"/> so you do not have to use the static <see cref="String"/>
	/// factory method
	/// </summary>
	public static implicit operator PostData(string literalString) => String(literalString);

	private class PostDataString : PostData
	{
		private readonly string _literalString;

		protected internal PostDataString(string item)
		{
			_literalString = item;
			Type = PostType.LiteralString;
		}

		public override void Write(Stream writableStream, ITransportConfiguration settings, bool disableDirectStreaming)
		{
			if (string.IsNullOrEmpty(_literalString)) return;

			MemoryStream? buffer = null;

			var stringBytes = WrittenBytes ?? _literalString.Utf8Bytes();
			WrittenBytes ??= stringBytes;
			if (stringBytes is not null)
			{
				if (!disableDirectStreaming)
					writableStream.Write(stringBytes, 0, stringBytes.Length);
				else
					buffer = settings.MemoryStreamFactory.Create(stringBytes);
			}

			FinishStream(writableStream, buffer, disableDirectStreaming);
		}

		public override async Task WriteAsync(Stream writableStream, ITransportConfiguration settings, bool disableDirectStreaming, CancellationToken cancellationToken)
		{
			if (string.IsNullOrEmpty(_literalString)) return;

			MemoryStream? buffer = null;

			var stringBytes = WrittenBytes ?? _literalString.Utf8Bytes();
			WrittenBytes ??= stringBytes;
			if (stringBytes is not null)
			{
				if (!disableDirectStreaming)
					await writableStream.WriteAsync(stringBytes, 0, stringBytes.Length, cancellationToken)
						.ConfigureAwait(false);
				else
					buffer = settings.MemoryStreamFactory.Create(stringBytes);
			}

			await FinishStreamAsync(writableStream, buffer, disableDirectStreaming, cancellationToken).ConfigureAwait(false);
		}
	}
}
