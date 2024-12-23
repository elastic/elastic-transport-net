// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Elastic.Transport;

/// <summary>
/// Represents the data the the user wishes to send over the wire. This abstract base class exposes
/// static factory methods to help you create wrap the various types of data we support.
/// <para></para>
/// <para>For raw bytes use <see cref="Bytes"/></para>
/// <para>For raw string use <see cref="String"/></para>
/// <para>To serialize an object use <see cref="Serializable{T}"/></para>
/// <para>For <see cref="ReadOnlyMemory{T}"/> use <see cref="ReadOnlyMemory{T}"/></para>
/// <para>To write your object directly to <see cref="Stream"/> using a handler use <see cref="StreamHandler{T}"/></para>
/// <para>Multiline json is supported to using  <see cref="MultiJson{T}(System.Collections.Generic.IEnumerable{T})"/></para>
/// <para>and  <see cref="MultiJson(System.Collections.Generic.IEnumerable{string})"/></para>
/// </summary>
public abstract partial class PostData
{
	/// <summary>
	/// The buffer size to use when calling <see cref="Stream.CopyTo(System.IO.Stream, int)"/>
	/// </summary>
	// ReSharper disable once MemberCanBePrivate.Global
	protected const int BufferSize = 81920;

	/// <summary> A static byte[] that hols a single new line feed </summary>
	// ReSharper disable once MemberCanBePrivate.Global
	protected static readonly byte[] NewLineByteArray = {(byte) '\n'};

	/// <summary> Reports the data this instance is wrapping </summary>
	// ReSharper disable once MemberCanBeProtected.Global
	public PostType Type { get; private set; }

	/// <summary>
	/// If <see cref="IRequestConfiguration.DisableDirectStreaming" /> is set to true, this will hold the buffered data after <see cref="Write"/>
	/// or <see cref="WriteAsync"/> is called
	/// </summary>
	public byte[]? WrittenBytes { get; private set; }

	/// <summary> A static instance that represents a body with no data </summary>
	// ReSharper disable once UnusedMember.Global
	public static PostData Empty => new PostDataString(string.Empty);

	/// <summary>
	/// Implementations of <see cref="PostData"/> are expected to implement writing the data they hold to
	/// <paramref name="writableStream"/>
	/// </summary>
	public abstract void Write(Stream writableStream, ITransportConfiguration settings, bool disableDirectStreaming);

	/// <summary>
	/// Implementations of <see cref="PostData"/> are expected to implement writing the data they hold to
	/// <paramref name="writableStream"/>
	/// </summary>
	public abstract Task WriteAsync(Stream writableStream, ITransportConfiguration settings, bool disableDirectStreaming, CancellationToken cancellationToken);

	/// <summary>
	/// byte[] implicitly converts to <see cref="PostData"/> so you do not have to use the static <see cref="Bytes"/>
	/// factory method
	/// </summary>
	public static implicit operator PostData(byte[] byteArray) => Bytes(byteArray);

	/// <summary>
	/// Buffers the stream if direct streaming is disabled.
	/// </summary>
	/// <param name="memoryStreamFactory">Factory to create memory streams.</param>
	/// <param name="disableDirectStreaming">Flag indicating whether direct streaming is disabled.</param>
	/// <param name="buffer">Reference to the buffer memory stream.</param>
	/// <param name="stream">Reference to the stream to write to.</param>
	protected void BufferIfNeeded(MemoryStreamFactory memoryStreamFactory, bool disableDirectStreaming, ref MemoryStream buffer, ref Stream stream)
	{
		if (!disableDirectStreaming) return;

		buffer = memoryStreamFactory.Create();
		stream = buffer;
	}

	/// <summary>
	/// Implementation of <see cref="Write"/> may call this to make sure <paramref name="buffer"/> makes it to <see cref="WrittenBytes"/>
	/// if <see cref="IRequestConfiguration.DisableDirectStreaming"/> requests to buffer the data.
	/// </summary>
	protected void FinishStream(Stream writableStream, MemoryStream? buffer, bool disableDirectStreaming)
	{
		if (buffer == null || !disableDirectStreaming) return;

		buffer.Position = 0;
		buffer.CopyTo(writableStream, BufferSize);
		WrittenBytes ??= buffer.ToArray();
		buffer.Dispose();
	}

	/// <summary>
	/// Implementation of <see cref="WriteAsync"/> may call this to make sure <paramref name="buffer"/> makes it to <see cref="WrittenBytes"/>
	/// if <see cref="IRequestConfiguration.DisableDirectStreaming"/> requests to buffer the data.
	/// </summary>
	protected async
#if !NETSTANDARD2_0 && !NETFRAMEWORK
		ValueTask
#else
		Task
#endif
		FinishStreamAsync(Stream writableStream, MemoryStream? buffer, bool disableDirectStreaming, CancellationToken ctx)
	{
		if (buffer == null || !disableDirectStreaming) return;

		buffer.Position = 0;
		await buffer.CopyToAsync(writableStream, BufferSize, ctx).ConfigureAwait(false);
		WrittenBytes ??= buffer.ToArray();
#if NET
		await buffer.DisposeAsync().ConfigureAwait(false);
#else
		buffer.Dispose();
#endif
	}
}
