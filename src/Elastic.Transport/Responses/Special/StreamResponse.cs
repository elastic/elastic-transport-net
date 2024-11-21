// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.IO;

namespace Elastic.Transport;

/// <summary>
/// A response that exposes the response <see cref="TransportResponse{T}.Body"/> as <see cref="Stream"/>.
/// <para>
/// <strong>MUST</strong> be disposed after use to ensure the HTTP connection is freed for reuse.
/// </para>
/// </summary>
public class StreamResponse : TransportResponse<Stream>, IDisposable
{
	private bool _disposed;

	/// <summary>
	/// The MIME type of the response, if present.
	/// </summary>
	public string ContentType { get; }

	/// <inheritdoc cref="StreamResponse"/>
	public StreamResponse()
	{
		Body = Stream.Null;
		ContentType = string.Empty;
	}

	/// <inheritdoc cref="StreamResponse"/>
	public StreamResponse(Stream body, string? contentType)
	{
		Body = body;
		ContentType = contentType ?? string.Empty;
	}

	internal override bool LeaveOpen => true;

	/// <summary>
	/// Disposes the underlying stream.
	/// </summary>
	/// <param name="disposing"></param>
	protected virtual void Dispose(bool disposing)
	{
		if (!_disposed)
		{
			if (disposing)
			{
				Body.Dispose();

				if (LinkedDisposables is not null)
				{
					foreach (var disposable in LinkedDisposables)
						disposable?.Dispose();
				}
			}

			_disposed = true;
		}
	}

	/// <summary>
	/// Disposes the underlying stream.
	/// </summary>
	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}
}
