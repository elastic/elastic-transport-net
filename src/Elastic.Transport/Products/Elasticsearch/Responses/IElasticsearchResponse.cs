// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;

namespace Elastic.Transport.Products.Elasticsearch;

/// <summary>
/// Common interface for Elasticsearch responses, providing unified access to error handling,
/// warnings, and response validation across all response types.
/// </summary>
public interface IElasticsearchResponse
{
	/// <summary>
	/// Whether the response is considered successful.
	/// </summary>
	bool IsValidResponse { get; }

	/// <summary>
	/// A collection of warnings returned from Elasticsearch.
	/// <para>Used to provide server warnings, for example, when the request uses an API feature that is marked as deprecated.</para>
	/// </summary>
	IEnumerable<string> ElasticsearchWarnings { get; }

	/// <summary>
	/// The server error, if any, returned by Elasticsearch.
	/// </summary>
	ElasticsearchServerError? ElasticsearchServerError { get; }

	/// <summary>
	/// Debug information about the request and response.
	/// </summary>
	string DebugInformation { get; }

	/// <summary>
	/// Attempts to retrieve the original exception that occurred during the request.
	/// </summary>
	/// <param name="exception">The original exception, if one occurred.</param>
	/// <returns><c>true</c> if an original exception was found; otherwise, <c>false</c>.</returns>
	bool TryGetOriginalException(out Exception? exception);
}
