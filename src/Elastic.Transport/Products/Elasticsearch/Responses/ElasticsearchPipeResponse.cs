// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

#if NET10_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;

namespace Elastic.Transport.Products.Elasticsearch;

/// <summary>
/// A response that exposes the response as a <see cref="PipeReader"/> with Elasticsearch error handling.
/// <para>
/// <strong>MUST</strong> be disposed after use to ensure the HTTP connection is freed for reuse.
/// </para>
/// <para>Provides <see cref="IsValidResponse"/>, <see cref="ElasticsearchWarnings"/>,
/// and <see cref="ElasticsearchServerError"/> in addition to the pipe reader body.</para>
/// </summary>
public sealed class ElasticsearchPipeResponse : PipeResponseBase, IElasticsearchResponse
{
	/// <inheritdoc cref="ElasticsearchPipeResponse"/>
	public ElasticsearchPipeResponse() : this(Stream.Null, string.Empty) { }

	/// <inheritdoc cref="ElasticsearchPipeResponse"/>
	public ElasticsearchPipeResponse(Stream responseStream, string? contentType) : base(responseStream, contentType) { }

	/// <summary>
	/// The response body as a <see cref="PipeReader"/>.
	/// </summary>
	public PipeReader Body => Pipe;

	/// <inheritdoc />
	public ElasticsearchServerError? ElasticsearchServerError => ElasticsearchResponseHelper.GetElasticsearchError(ApiCallDetails);

	/// <inheritdoc />
	public bool IsValidResponse => ElasticsearchResponseHelper.IsValidResponse(ApiCallDetails);

	/// <inheritdoc />
	public IEnumerable<string> ElasticsearchWarnings => ElasticsearchResponseHelper.GetElasticsearchWarnings(ApiCallDetails);

	/// <inheritdoc />
	public string DebugInformation => ElasticsearchResponseHelper.GetDebugInformation(IsValidResponse, ApiCallDetails);

	/// <inheritdoc />
	public bool TryGetOriginalException(out Exception? exception) =>
		ElasticsearchResponseHelper.TryGetOriginalException(ApiCallDetails, out exception);

	/// <inheritdoc />
	public override string ToString() => DebugInformation;
}
#endif
