// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

#if NET10_0_OR_GREATER
using System.IO;
using System.IO.Pipelines;

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
public sealed class PipeResponse : PipeResponseBase
{
	/// <inheritdoc cref="PipeResponse"/>
	public PipeResponse() : this(Stream.Null, string.Empty) { }

	/// <inheritdoc cref="PipeResponse"/>
	public PipeResponse(Stream responseStream, string? contentType) : base(responseStream, contentType) { }

	/// <summary>
	/// The response body as a <see cref="PipeReader"/>.
	/// <para>
	/// Can be used directly with <see cref="System.Text.Json.JsonSerializer.DeserializeAsync{TValue}(PipeReader, System.Text.Json.JsonSerializerOptions?, System.Threading.CancellationToken)"/>
	/// or <see cref="System.Text.Json.JsonSerializer.DeserializeAsyncEnumerable{TValue}(PipeReader, System.Text.Json.JsonSerializerOptions?, System.Threading.CancellationToken)"/>
	/// for efficient deserialization without intermediate buffering.
	/// </para>
	/// </summary>
	public PipeReader Body => Pipe;
}
#endif
