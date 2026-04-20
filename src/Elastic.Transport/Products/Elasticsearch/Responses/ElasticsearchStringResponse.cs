// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;

namespace Elastic.Transport.Products.Elasticsearch;

/// <summary>
/// A response that exposes the response body as a <see cref="string"/> with Elasticsearch error handling.
/// <para>Provides <see cref="IsValidResponse"/>, <see cref="ElasticsearchWarnings"/>,
/// and <see cref="ElasticsearchServerError"/> in addition to the string body.</para>
/// </summary>
public sealed class ElasticsearchStringResponse : StringResponseBase, IElasticsearchResponse
{
	/// <inheritdoc cref="ElasticsearchStringResponse"/>
	public ElasticsearchStringResponse() { }

	/// <inheritdoc cref="ElasticsearchStringResponse"/>
	public ElasticsearchStringResponse(string body) : base(body) { }

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
