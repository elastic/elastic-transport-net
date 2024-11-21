// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.IO;
using Elastic.Transport.Extensions;

namespace Elastic.Transport;

/// <summary>
/// A base class for implementing responses that access the raw response stream.
/// </summary>
public abstract class StreamResponseBase : TransportResponse, IDisposable
{
	/// <inheritdoc/>
	protected internal override bool LeaveOpen => true;

	/// <summary>
	/// The raw response stream from the HTTP layer.
	/// </summary>
	/// <remarks>
	/// <b>MUST</b> be disposed to release the underlying HTTP connection for reuse.
	/// </remarks>
	protected Stream Stream { get; }

	/// <summary>
	/// Indicates that the response has been disposed and it is not longer safe to access the stream.
	/// </summary>
	protected bool Disposed { get; private set; }

	/// <inheritdoc cref="StreamResponseBase"/>
	public StreamResponseBase(Stream responseStream)
	{
		responseStream.ThrowIfNull(nameof(responseStream));
		Stream = responseStream;
	}

	/// <summary>
	/// Disposes the underlying stream.
	/// </summary>
	/// <param name="disposing"></param>
	protected virtual void Dispose(bool disposing)
	{
		if (!Disposed)
		{
			if (disposing)
			{
				Stream?.Dispose();

				if (LinkedDisposables is not null)
				{
					foreach (var disposable in LinkedDisposables)
						disposable?.Dispose();
				}
			}

			Disposed = true;
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
