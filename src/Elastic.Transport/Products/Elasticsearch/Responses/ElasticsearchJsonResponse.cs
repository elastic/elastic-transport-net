// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace Elastic.Transport.Products.Elasticsearch;

/// <summary>
/// A response backed by <see cref="JsonNode"/> with Elasticsearch error handling.
/// <para>Provides <see cref="IsValidResponse"/>, <see cref="ElasticsearchWarnings"/>,
/// and <see cref="ElasticsearchServerError"/> in addition to JSON DOM access.</para>
/// </summary>
public sealed class ElasticsearchJsonResponse : JsonResponseBase, IElasticsearchResponse, IElasticsearchResponseSetter
{
	/// <inheritdoc cref="ElasticsearchJsonResponse"/>
	public ElasticsearchJsonResponse() { }

	/// <inheritdoc cref="ElasticsearchJsonResponse"/>
	public ElasticsearchJsonResponse(JsonNode node) : base(node) { }

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
