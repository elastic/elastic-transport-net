// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

namespace Elastic.Transport;

/// <summary>
/// Base class for responses that expose the response body as a <see cref="string"/>.
/// </summary>
public abstract class StringResponseBase : TransportResponse<string>
{
	/// <inheritdoc cref="StringResponseBase"/>
	protected StringResponseBase() => Body = string.Empty;

	/// <inheritdoc cref="StringResponseBase"/>
	protected StringResponseBase(string body) => Body = body;
}
