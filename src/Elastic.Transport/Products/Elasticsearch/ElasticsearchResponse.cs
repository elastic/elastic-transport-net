// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace Elastic.Transport.Products.Elasticsearch;

/// <summary>
/// Base response for Elasticsearch responses.
/// </summary>
public abstract class ElasticsearchResponse : TransportResponse, IElasticsearchResponse
{
	/// <inheritdoc />
	[JsonIgnore]
	public IEnumerable<string> ElasticsearchWarnings =>
		ElasticsearchResponseHelper.GetElasticsearchWarnings(ApiCallDetails);

	/// <inheritdoc />
	[JsonIgnore]
	public string DebugInformation =>
		ElasticsearchResponseHelper.GetDebugInformation(IsValidResponse, ApiCallDetails, ElasticsearchServerError);

	/// <inheritdoc />
	[JsonIgnore]
	public virtual bool IsValidResponse =>
		ElasticsearchResponseHelper.IsValidResponse(ApiCallDetails, ElasticsearchServerError);

	/// <inheritdoc />
	[JsonIgnore]
	public ElasticsearchServerError? ElasticsearchServerError { get; internal set; }

	/// <inheritdoc />
	public bool TryGetOriginalException(out Exception? exception) =>
		ElasticsearchResponseHelper.TryGetOriginalException(ApiCallDetails, out exception);

	/// <summary>Subclasses can override this to provide more information on why a call is not valid.</summary>
	protected virtual void DebugIsValid(StringBuilder sb) { }

	/// <summary>
	/// A custom <see cref="ToString"/> implementation that returns <see cref="DebugInformation"/>
	/// </summary>
	public override string ToString() => DebugInformation;
}
