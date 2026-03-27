// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace Elastic.Transport.Products.Elasticsearch;

/// <summary>
/// Shared error extraction logic for Elasticsearch responses.
/// </summary>
internal static class ElasticsearchErrorHelper
{
	/// <summary>
	/// Attempts to deserialize an <see cref="ElasticsearchServerError"/> from a seekable stream.
	/// Does not modify stream position on failure; resets to 0 is caller's responsibility.
	/// </summary>
	public static bool TryGetError(BoundConfiguration boundConfiguration, Stream responseStream, out ElasticsearchServerError? error)
	{
		Debug.Assert(responseStream.CanSeek);

		error = null;

		try
		{
			error = boundConfiguration.ConnectionSettings.RequestResponseSerializer.Deserialize<ElasticsearchServerError>(responseStream);
			return error is not null;
		}
		catch (JsonException)
		{
			// We'll try the original response type if the error serialization fails
		}

		return false;
	}
}
