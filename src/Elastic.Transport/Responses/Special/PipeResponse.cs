// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

#if NET10_0_OR_GREATER
using System;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Elastic.Transport.Extensions;

namespace Elastic.Transport;

/// <summary>
/// A response that exposes the response as a <see cref="PipeReader"/>.
/// <para>
/// This leverages .NET 10's support for <see cref="System.Text.Json.JsonSerializer"/> deserialization
/// directly from <see cref="PipeReader"/>, avoiding intermediate Stream conversions.
/// </para>
/// <para>
/// <strong>MUST</strong> be disposed after use to ensure the HTTP connection is freed for reuse.
/// </para>
/// </summary>
public sealed class PipeResponse : TransportResponse, IAsyncDisposable, IDisposable
{
	private readonly Stream _stream;
	private bool _disposed;

	/// <inheritdoc cref="PipeResponse"/>
	public PipeResponse() : this(Stream.Null, string.Empty) { }

	/// <inheritdoc cref="PipeResponse"/>
	public PipeResponse(Stream responseStream, string? contentType)
	{
		responseStream.ThrowIfNull(nameof(responseStream));
		_stream = responseStream;
		ContentType = contentType ?? string.Empty;
		Body = PipeReader.Create(_stream, new StreamPipeReaderOptions(leaveOpen: false));
	}

	/// <summary>
	/// The MIME type of the response, if present.
	/// </summary>
	public string ContentType { get; }

	/// <summary>
	/// The response body as a <see cref="PipeReader"/>.
	/// <para>
	/// Can be used directly with <see cref="System.Text.Json.JsonSerializer.DeserializeAsync{TValue}(PipeReader, System.Text.Json.JsonSerializerOptions?, CancellationToken)"/>
	/// or <see cref="System.Text.Json.JsonSerializer.DeserializeAsyncEnumerable{TValue}(PipeReader, System.Text.Json.JsonSerializerOptions?, CancellationToken)"/>
	/// for efficient deserialization without intermediate buffering.
	/// </para>
	/// </summary>
	public PipeReader Body { get; }

	/// <inheritdoc/>
	protected internal override bool LeaveOpen => true;

	/// <summary>
	/// Copies the response body directly to a <see cref="PipeWriter"/>.
	/// <para>
	/// This is ideal for forwarding Elasticsearch responses directly to ASP.NET Core's
	/// <c>HttpContext.Response.BodyWriter</c> without intermediate buffering.
	/// </para>
	/// </summary>
	/// <param name="destination">The <see cref="PipeWriter"/> to write to (e.g., <c>HttpContext.Response.BodyWriter</c>)</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <example>
	/// <code>
	/// // In an ASP.NET Core minimal API or controller:
	/// app.MapGet("/search", async (HttpContext context, ITransport transport) =>
	/// {
	///     await using var response = await transport.GetAsync&lt;PipeResponse&gt;(path);
	///     context.Response.ContentType = response.ContentType;
	///     await response.CopyToAsync(context.Response.BodyWriter, context.RequestAborted);
	/// });
	/// </code>
	/// </example>
	public async Task CopyToAsync(PipeWriter destination, CancellationToken cancellationToken = default)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		while (true)
		{
			var result = await Body.ReadAsync(cancellationToken).ConfigureAwait(false);
			var buffer = result.Buffer;

			try
			{
				foreach (var segment in buffer)
				{
					var destBuffer = destination.GetMemory(segment.Length);
					segment.CopyTo(destBuffer);
					destination.Advance(segment.Length);
				}

				var flushResult = await destination.FlushAsync(cancellationToken).ConfigureAwait(false);

				if (result.IsCompleted || flushResult.IsCompleted)
					break;
			}
			finally
			{
				Body.AdvanceTo(buffer.End);
			}
		}
	}

	/// <summary>
	/// Disposes the underlying stream and completes the <see cref="PipeReader"/>.
	/// </summary>
	public void Dispose()
	{
		if (_disposed)
			return;

		Body.Complete();
		_stream.Dispose();

		if (LinkedDisposables is not null)
		{
			foreach (var disposable in LinkedDisposables)
				disposable?.Dispose();
		}

		_disposed = true;
		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// Asynchronously disposes the underlying stream and completes the <see cref="PipeReader"/>.
	/// </summary>
	public async ValueTask DisposeAsync()
	{
		if (_disposed)
			return;

		await Body.CompleteAsync().ConfigureAwait(false);
		await _stream.DisposeAsync().ConfigureAwait(false);

		if (LinkedDisposables is not null)
		{
			foreach (var disposable in LinkedDisposables)
				disposable?.Dispose();
		}

		_disposed = true;
		GC.SuppressFinalize(this);
	}
}
#endif
