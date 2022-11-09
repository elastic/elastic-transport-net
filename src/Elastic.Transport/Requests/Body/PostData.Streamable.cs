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
	/// Create an instance of serializable data <paramref name="state"/>. This state is then passed to <paramref name="syncWriter"/>
	/// and <paramref name="asyncWriter"/> along with the <see cref="Stream"/> to write to. Both will need to be supplied in order to
	/// support both <see cref="Write"/> and <see cref="WriteAsync"/>
	/// </summary>
	/// <param name="state">The object we want to serialize later on</param>
	/// <param name="syncWriter">A func receiving <paramref name="state"/> and a <see cref="Stream"/> to write to</param>
	/// <param name="asyncWriter">A func receiving <paramref name="state"/> and a <see cref="Stream"/> to write to</param>
	public static PostData StreamHandler<T>(T state, Action<T, Stream> syncWriter,
		Func<T, Stream, CancellationToken, Task> asyncWriter) =>
		new StreamableData<T>(state, syncWriter, asyncWriter);

	/// <summary>
	/// Represents an instance of <see cref="PostData"/> that can handle <see cref="PostType.StreamHandler"/>.
	/// Allows users full control over how they want to write data to the stream.
	/// </summary>
	/// <typeparam name="T">The data or a state object used during writing, passed to the handlers to avoid boxing</typeparam>
	private class StreamableData<T> : PostData
	{
		private readonly T _state;
		private readonly Action<T, Stream> _syncWriter;
		private readonly Func<T, Stream, CancellationToken, Task> _asyncWriter;

		public StreamableData(T state, Action<T, Stream> syncWriter,
			Func<T, Stream, CancellationToken, Task> asyncWriter)
		{
			_state = state;
			const string message = "PostData.StreamHandler needs to handle both synchronous and async paths";
			_syncWriter = syncWriter ?? throw new ArgumentNullException(nameof(syncWriter), message);
			_asyncWriter = asyncWriter ?? throw new ArgumentNullException(nameof(asyncWriter), message);
			if (_syncWriter == null || _asyncWriter == null)
				throw new ArgumentNullException();
			Type = PostType.StreamHandler;
		}

		public override void Write(Stream writableStream, ITransportConfiguration settings)
		{
			MemoryStream buffer = null;
			var stream = writableStream;
			BufferIfNeeded(settings, ref buffer, ref stream);
			_syncWriter(_state, stream);
			FinishStream(writableStream, buffer, settings);
		}

		public override async Task WriteAsync(Stream writableStream, ITransportConfiguration settings,
			CancellationToken cancellationToken)
		{
			MemoryStream buffer = null;
			var stream = writableStream;
			BufferIfNeeded(settings, ref buffer, ref stream);
			await _asyncWriter(_state, stream, cancellationToken).ConfigureAwait(false);
			await FinishStreamAsync(writableStream, buffer, settings, cancellationToken).ConfigureAwait(false);
		}
	}
}
