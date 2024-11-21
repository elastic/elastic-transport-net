// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.IO;

namespace Elastic.Transport;

/// <summary>
/// A response that exposes the response as a <see cref="Stream"/>.
/// <para>
/// <strong>MUST</strong> be disposed after use to ensure the HTTP connection is freed for reuse.
/// </para>
/// </summary>
public sealed class StreamResponse : StreamResponseBase, IDisposable
{
	/// <inheritdoc cref="StreamResponse"/>
	public StreamResponse() : base(Stream.Null) =>
		ContentType = string.Empty;

	/// <inheritdoc cref="StreamResponse"/>
	public StreamResponse(Stream body, string? contentType) : base(body) =>
		ContentType = contentType ?? string.Empty;

	/// <summary>
	/// The MIME type of the response, if present.
	/// </summary>
	public string ContentType { get; }

	/// <summary>
	/// The raw response stream.
	/// </summary>
	public Stream Body => Stream;

	/// <inheritdoc/>
	protected internal override bool LeaveOpen => true;
}
