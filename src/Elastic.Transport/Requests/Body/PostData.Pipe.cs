// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

#if NET10_0_OR_GREATER
using System;
using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace Elastic.Transport;

public abstract partial class PostData
{
	/// <summary>
	/// Create a <see cref="PostData"/> instance that reads from an existing <see cref="PipeReader"/>.
	/// <para>
	/// This is ideal for forwarding request bodies from ASP.NET Core's <c>HttpContext.Request.BodyReader</c>
	/// directly to Elasticsearch without intermediate buffering.
	/// </para>
	/// </summary>
	/// <param name="pipeReader">The <see cref="PipeReader"/> to read data from</param>
	/// <example>
	/// <code>
	/// // In an ASP.NET Core minimal API or controller:
	/// app.MapPost("/forward", async (HttpContext context, ITransport transport) =>
	/// {
	///     var postData = PostData.PipeReader(context.Request.BodyReader);
	///     var response = await transport.PostAsync&lt;StringResponse&gt;(path, postData);
	///     return Results.Ok(response.Body);
	/// });
	/// </code>
	/// </example>
	public static PostData PipeReader(PipeReader pipeReader) => new PipeReaderData(pipeReader);

	/// <summary>
	/// Create an instance of serializable data <paramref name="state"/>. This state is then passed to <paramref name="asyncWriter"/>
	/// along with a <see cref="PipeWriter"/> to write to.
	/// <para>
	/// This leverages .NET 10's support for <see cref="System.Text.Json.JsonSerializer.SerializeAsync{TValue}(PipeWriter, TValue, System.Text.Json.JsonSerializerOptions?, CancellationToken)"/>
	/// for efficient serialization directly to a <see cref="PipeWriter"/>.
	/// </para>
	/// </summary>
	/// <param name="state">The object we want to serialize later on</param>
	/// <param name="asyncWriter">A func receiving <paramref name="state"/> and a <see cref="PipeWriter"/> to write to</param>
	/// <typeparam name="T">The type of the state object</typeparam>
	/// <example>
	/// <code>
	/// var myData = new MyDocument { Title = "Hello" };
	/// var postData = PostData.PipeWriter(myData, async (data, writer, ct) =>
	/// {
	///     await JsonSerializer.SerializeAsync(writer, data, cancellationToken: ct);
	/// });
	/// </code>
	/// </example>
	public static PostData PipeWriter<T>(T state, Func<T, PipeWriter, CancellationToken, Task> asyncWriter) =>
		new PipeWriterData<T>(state, asyncWriter);

	/// <summary>
	/// Represents an instance of <see cref="PostData"/> that reads from an existing <see cref="PipeReader"/>.
	/// Ideal for forwarding ASP.NET Core request bodies directly to Elasticsearch.
	/// </summary>
	private class PipeReaderData : PostData
	{
		private readonly PipeReader _pipeReader;

		public PipeReaderData(PipeReader pipeReader)
		{
			_pipeReader = pipeReader ?? throw new ArgumentNullException(nameof(pipeReader));
			Type = PostType.Pipe;
		}

		// PipeReader is inherently async, so we run the async path synchronously
		public override void Write(Stream writableStream, ITransportConfiguration settings, bool disableDirectStreaming) =>
			WriteAsync(writableStream, settings, disableDirectStreaming, CancellationToken.None)
				.ConfigureAwait(false)
				.GetAwaiter()
				.GetResult();

		public override async Task WriteAsync(Stream writableStream, ITransportConfiguration settings, bool disableDirectStreaming, CancellationToken cancellationToken)
		{
			if (disableDirectStreaming)
			{
				// When buffering is required, we need to capture all bytes first
				var bufferStream = settings.MemoryStreamFactory.Create();
				try
				{
					await CopyPipeReaderToStreamAsync(_pipeReader, bufferStream, cancellationToken).ConfigureAwait(false);

					// Capture the written bytes
					WrittenBytes = bufferStream.ToArray();

					// Write the buffered data to the actual stream
					bufferStream.Position = 0;
					await bufferStream.CopyToAsync(writableStream, BufferSize, cancellationToken).ConfigureAwait(false);
				}
				finally
				{
					await bufferStream.DisposeAsync().ConfigureAwait(false);
				}
			}
			else
			{
				// Direct streaming - copy directly from PipeReader to output stream
				await CopyPipeReaderToStreamAsync(_pipeReader, writableStream, cancellationToken).ConfigureAwait(false);
			}
		}

		private static async Task CopyPipeReaderToStreamAsync(PipeReader reader, Stream destination, CancellationToken cancellationToken)
		{
			while (true)
			{
				var result = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
				var buffer = result.Buffer;

				try
				{
					foreach (var segment in buffer)
					{
						await destination.WriteAsync(segment, cancellationToken).ConfigureAwait(false);
					}

					if (result.IsCompleted)
						break;
				}
				finally
				{
					reader.AdvanceTo(buffer.End);
				}
			}

			await reader.CompleteAsync().ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Represents an instance of <see cref="PostData"/> that can handle <see cref="PostType.Pipe"/>.
	/// Allows users to write data using <see cref="PipeWriter"/> for efficient streaming serialization.
	/// </summary>
	/// <typeparam name="T">The data or a state object used during writing, passed to the handlers to avoid boxing</typeparam>
	private class PipeWriterData<T> : PostData
	{
		private readonly T _state;
		private readonly Func<T, PipeWriter, CancellationToken, Task> _asyncWriter;

		public PipeWriterData(T state, Func<T, PipeWriter, CancellationToken, Task> asyncWriter)
		{
			_state = state;
			_asyncWriter = asyncWriter ?? throw new ArgumentNullException(nameof(asyncWriter), "PostData.PipeWriter requires an async writer handler");
			Type = PostType.Pipe;
		}

		// PipeWriter is inherently async, so we run the async path synchronously
		// This is not ideal but maintains compatibility with the sync API
		public override void Write(Stream writableStream, ITransportConfiguration settings, bool disableDirectStreaming) =>
			WriteAsync(writableStream, settings, disableDirectStreaming, CancellationToken.None)
				.ConfigureAwait(false)
				.GetAwaiter()
				.GetResult();

		public override async Task WriteAsync(Stream writableStream, ITransportConfiguration settings, bool disableDirectStreaming, CancellationToken cancellationToken)
		{
			if (disableDirectStreaming)
			{
				// When buffering is required, we need to capture the bytes
				var bufferStream = settings.MemoryStreamFactory.Create();
				try
				{
					var pipeWriter = System.IO.Pipelines.PipeWriter.Create(bufferStream, new StreamPipeWriterOptions(leaveOpen: true));
					await _asyncWriter(_state, pipeWriter, cancellationToken).ConfigureAwait(false);
					await pipeWriter.CompleteAsync().ConfigureAwait(false);

					// Capture the written bytes
					WrittenBytes = bufferStream.ToArray();

					// Write the buffered data to the actual stream
					bufferStream.Position = 0;
					await bufferStream.CopyToAsync(writableStream, BufferSize, cancellationToken).ConfigureAwait(false);
				}
				finally
				{
					await bufferStream.DisposeAsync().ConfigureAwait(false);
				}
			}
			else
			{
				// Direct streaming - write directly to the output stream via PipeWriter
				var pipeWriter = System.IO.Pipelines.PipeWriter.Create(writableStream, new StreamPipeWriterOptions(leaveOpen: true));
				await _asyncWriter(_state, pipeWriter, cancellationToken).ConfigureAwait(false);
				await pipeWriter.CompleteAsync().ConfigureAwait(false);
			}
		}
	}
}
#endif
