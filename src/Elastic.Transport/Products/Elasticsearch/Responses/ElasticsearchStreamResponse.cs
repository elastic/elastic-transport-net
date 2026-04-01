// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.IO;

namespace Elastic.Transport.Products.Elasticsearch;

/// <summary>
/// A response that exposes the response as a <see cref="Stream"/> with Elasticsearch error handling.
/// <para>
/// <strong>MUST</strong> be disposed after use to ensure the HTTP connection is freed for reuse.
/// </para>
/// <para>Provides <see cref="IsValidResponse"/>, <see cref="ElasticsearchWarnings"/>,
/// and <see cref="ElasticsearchServerError"/> in addition to the stream body.</para>
/// </summary>
public sealed class ElasticsearchStreamResponse : StreamResponseBase, IElasticsearchResponse, IElasticsearchResponseSetter
{
	/// <inheritdoc cref="ElasticsearchStreamResponse"/>
	public ElasticsearchStreamResponse() : base(Stream.Null) { }

	/// <inheritdoc cref="ElasticsearchStreamResponse"/>
	public ElasticsearchStreamResponse(Stream body, string? contentType) : base(body, contentType) { }

	/// <summary>
	/// The raw response stream.
	/// </summary>
	public Stream Body => Stream;

	/// <inheritdoc />
	public ElasticsearchServerError? ElasticsearchServerError { get; internal set; }
	ElasticsearchServerError? IElasticsearchResponseSetter.ElasticsearchServerError { set => ElasticsearchServerError = value; }

	/// <inheritdoc />
	public bool IsValidResponse => ElasticsearchResponseHelper.IsValidResponse(ApiCallDetails, ElasticsearchServerError);

	/// <inheritdoc />
	public IEnumerable<string> ElasticsearchWarnings => ElasticsearchResponseHelper.GetElasticsearchWarnings(ApiCallDetails);

	/// <inheritdoc />
	public string DebugInformation => ElasticsearchResponseHelper.GetDebugInformation(IsValidResponse, ApiCallDetails, ElasticsearchServerError);

	/// <inheritdoc />
	public bool TryGetOriginalException(out Exception? exception) =>
		ElasticsearchResponseHelper.TryGetOriginalException(ApiCallDetails, out exception);

	/// <inheritdoc />
	public override string ToString() => DebugInformation;
}
