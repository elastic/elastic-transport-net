// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.IO;

namespace Elastic.Transport;

/// <summary>
/// A response that exposes the response <see cref="TransportResponse{T}.Body"/> as <see cref="Stream"/>.
/// <para>
///		Must be disposed after use.
/// </para>
/// </summary>
public sealed class StreamResponse :
	TransportResponse<Stream>,
	IDisposable
{
	internal Action? Finalizer { get; set; }

	/// <summary>
	/// The MIME type of the response, if present.
	/// </summary>
	public string MimeType { get; }

	/// <inheritdoc cref="StreamResponse"/>
	public StreamResponse()
	{
		Body = Stream.Null;
		MimeType = string.Empty;
	}

	/// <inheritdoc cref="StreamResponse"/>
	public StreamResponse(Stream body, string? mimeType)
	{
		Body = body;
		MimeType = mimeType ?? string.Empty;
	}

	/// <inheritdoc cref="IDisposable.Dispose"/>
	public void Dispose()
	{
		Body.Dispose();
		Finalizer?.Invoke();
	}
}
