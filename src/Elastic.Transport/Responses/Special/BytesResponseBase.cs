// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

namespace Elastic.Transport;

/// <summary>
/// Base class for responses that expose the response body as a byte array.
/// </summary>
public abstract class BytesResponseBase : TransportResponse<byte[]>
{
	/// <inheritdoc cref="BytesResponseBase"/>
	protected BytesResponseBase() => Body = [];

	/// <inheritdoc cref="BytesResponseBase"/>
	protected BytesResponseBase(byte[] body) => Body = body;
}
