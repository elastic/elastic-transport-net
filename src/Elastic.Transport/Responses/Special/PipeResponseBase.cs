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
/// Base class for responses that expose the response as a <see cref="PipeReader"/>.
/// <para>
/// This leverages .NET 10's support for <see cref="System.Text.Json.JsonSerializer"/> deserialization
/// directly from <see cref="PipeReader"/>, avoiding intermediate Stream conversions.
/// </para>
/// <para>
/// <strong>MUST</strong> be disposed after use to ensure the HTTP connection is freed for reuse.
/// </para>
/// </summary>
public abstract class PipeResponseBase : TransportResponse, IAsyncDisposable, IDisposable
{
	private readonly Stream _stream;
	private bool _disposed;

	/// <inheritdoc cref="PipeResponseBase"/>
	protected PipeResponseBase(Stream responseStream, string? contentType)
	{
		responseStream.ThrowIfNull(nameof(responseStream));
		_stream = responseStream;
		ContentType = contentType ?? string.Empty;
		Pipe = PipeReader.Create(_stream, new StreamPipeReaderOptions(leaveOpen: false));
	}

	/// <summary>
	/// The MIME type of the response, if present.
	/// </summary>
	public string ContentType { get; }

	/// <summary>
	/// The response body as a <see cref="PipeReader"/>.
	/// </summary>
	protected PipeReader Pipe { get; }

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
	/// app.MapGet("/search", async (HttpContext context, ITransport transport) =&gt;
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
			var result = await Pipe.ReadAsync(cancellationToken).ConfigureAwait(false);
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
				Pipe.AdvanceTo(buffer.End);
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

		Pipe.Complete();
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

		await Pipe.CompleteAsync().ConfigureAwait(false);
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
