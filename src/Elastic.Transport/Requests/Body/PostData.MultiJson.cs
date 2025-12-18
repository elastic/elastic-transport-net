// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Elastic.Transport.Extensions;

namespace Elastic.Transport;

public abstract partial class PostData
{
	/// <summary>
	/// Create a <see cref="PostData"/> instance that will write the <paramref name="listOfString"/> as multiline/ndjson.
	/// </summary>
	public static PostData MultiJson(IEnumerable<string> listOfString) =>
		new PostDataMultiJson<string>(listOfString);

	/// <summary>
	/// Create a <see cref="PostData"/> instance that will serialize the <paramref name="listOfSerializables"/> as multiline/ndjson.
	/// </summary>
	public static PostData MultiJson<T>(IEnumerable<T> listOfSerializables) =>
		new PostDataMultiJson<T>(listOfSerializables);

	private class PostDataMultiJson<T> : PostData
	{
		private readonly IEnumerable<T>? _enumerableOfObject;
		private readonly IEnumerable<string>? _enumerableOfStrings;

		protected internal PostDataMultiJson(IEnumerable<string> item)
		{
			_enumerableOfStrings = item;
			Type = PostType.EnumerableOfString;
		}

		protected internal PostDataMultiJson(IEnumerable<T> item)
		{
			_enumerableOfObject = item;
			Type = PostType.EnumerableOfObject;
		}

		public override void Write(Stream writableStream, ITransportConfiguration settings, bool disableDirectStreaming)
		{
			if (Type is not PostType.EnumerableOfObject and not PostType.EnumerableOfString)
				throw new Exception(
					$"{nameof(PostDataMultiJson<>)} only does not support {nameof(PostType)}.{Type.ToStringFast()}");

			MemoryStream? buffer = null;
			var stream = writableStream;

			switch (Type)
			{
				case PostType.EnumerableOfString:
					{
						if (_enumerableOfStrings == null)
							return;

						using var enumerator = _enumerableOfStrings.GetEnumerator();
						if (!enumerator.MoveNext())
							return;

						BufferIfNeeded(settings.MemoryStreamFactory, disableDirectStreaming, ref buffer, ref stream);
						do
						{
							var bytes = enumerator.Current.Utf8Bytes();
							if (bytes is not null)
								stream.Write(bytes, 0, bytes.Length);
							stream.Write(NewLineByteArray, 0, 1);
						} while (enumerator.MoveNext());

						break;
					}
				case PostType.EnumerableOfObject:
					{
						if (_enumerableOfObject == null)
							return;

						using var enumerator = _enumerableOfObject.GetEnumerator();
						if (!enumerator.MoveNext())
							return;

						BufferIfNeeded(settings.MemoryStreamFactory, disableDirectStreaming, ref buffer, ref stream);
						do
						{
							var o = enumerator.Current;
							settings.RequestResponseSerializer.Serialize(o, stream, SerializationFormatting.None);
							stream.Write(NewLineByteArray, 0, 1);
						} while (enumerator.MoveNext());

						break;
					}
				default:
					throw new InvalidOperationException($"Unexpected PostType: {Type}");
			}

			FinishStream(writableStream, buffer, disableDirectStreaming);
		}

		public override async Task WriteAsync(Stream writableStream, ITransportConfiguration settings, bool disableDirectStreaming, CancellationToken cancellationToken)
		{
			if (Type is not PostType.EnumerableOfObject and not PostType.EnumerableOfString)
				throw new Exception(
					$"{nameof(PostDataMultiJson<>)} only does not support {nameof(PostType)}.{Type.ToStringFast()}");

			MemoryStream? buffer = null;
			var stream = writableStream;

			switch (Type)
			{
				case PostType.EnumerableOfString:
					{
						if (_enumerableOfStrings == null)
							return;

						using var enumerator = _enumerableOfStrings.GetEnumerator();
						if (!enumerator.MoveNext())
							return;

						BufferIfNeeded(settings.MemoryStreamFactory, disableDirectStreaming, ref buffer, ref stream);
						do
						{
							var bytes = enumerator.Current.Utf8Bytes();
							if (bytes is not null)
#if NETSTANDARD2_1_OR_GREATER || NET
								await stream.WriteAsync(bytes.AsMemory(), cancellationToken).ConfigureAwait(false);
#else
								await stream.WriteAsync(bytes, 0, bytes.Length, cancellationToken).ConfigureAwait(false);
#endif
#if NETSTANDARD2_1_OR_GREATER || NET
							await stream.WriteAsync(NewLineByteArray.AsMemory(), cancellationToken).ConfigureAwait(false);
#else
							await stream.WriteAsync(NewLineByteArray, 0, 1, cancellationToken).ConfigureAwait(false);
#endif
						} while (enumerator.MoveNext());

						break;
					}
				case PostType.EnumerableOfObject:
					{
						if (_enumerableOfObject == null)
							return;

						using var enumerator = _enumerableOfObject.GetEnumerator();
						if (!enumerator.MoveNext())
							return;

						BufferIfNeeded(settings.MemoryStreamFactory, disableDirectStreaming, ref buffer, ref stream);
						do
						{
							var o = enumerator.Current;
							await settings.RequestResponseSerializer.SerializeAsync(o, stream,
									SerializationFormatting.None, cancellationToken)
								.ConfigureAwait(false);
#if NETSTANDARD2_1_OR_GREATER || NET
							await stream.WriteAsync(NewLineByteArray.AsMemory(), cancellationToken).ConfigureAwait(false);
#else
							await stream.WriteAsync(NewLineByteArray, 0, 1, cancellationToken).ConfigureAwait(false);
#endif
						} while (enumerator.MoveNext());

						break;
					}
				default:
					throw new InvalidOperationException($"Unexpected PostType: {Type}");
			}

			await FinishStreamAsync(writableStream, buffer, disableDirectStreaming, cancellationToken).ConfigureAwait(false);
		}
	}
}
