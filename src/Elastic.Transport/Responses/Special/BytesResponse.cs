// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

namespace Elastic.Transport;

/// <summary>
/// A response that exposes the response <see cref="TransportResponse{T}.Body"/> as byte array.
/// </summary>
public sealed class BytesResponse : BytesResponseBase
{
	/// <inheritdoc cref="BytesResponse"/>
	public BytesResponse() { }

	/// <inheritdoc cref="BytesResponse"/>
	public BytesResponse(byte[] body) : base(body) { }
}
