// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

namespace Elastic.Transport;

/// <summary>
/// Injects metadata headers into all outgoing requests.
/// </summary>
public abstract class MetaHeaderProvider
{
	/// <summary>
	/// The list of all <see cref="MetaHeaderProducer"/>s for the provider.
	/// </summary>
	public abstract MetaHeaderProducer[] Producers { get; }
}

/// <summary>
/// Injects a metadata headers into all outgoing requests.
/// </summary>
public abstract class MetaHeaderProducer
{
	/// <summary>
	/// The header name.
	/// </summary>
	public abstract string HeaderName { get; }

	/// <summary>
	/// Produces the header value based on current outgoing <paramref name="requestData"/>.
	/// </summary>
	public abstract string ProduceHeaderValue(RequestData requestData);
}
