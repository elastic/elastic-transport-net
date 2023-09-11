// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

namespace Elastic.Transport;

/// <summary>
/// Injects a metadata header into all outgoing requests
/// </summary>
public abstract class MetaHeaderProvider
{
	/// <summary>Header name </summary>
	public abstract string HeaderName { get; }

	/// <summary> Produces the header value based on current outgoing <paramref name="requestData"/> </summary>
	public abstract string ProduceHeaderValue(RequestData requestData);
}
