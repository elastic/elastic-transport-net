// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Text.Json.Serialization;

namespace Elastic.Transport
{
	/// <summary>
	/// A response from an Elastic product including details about the request/response life cycle. Base class for the built in low level response
	/// types, <see cref="StringResponse"/>, <see cref="BytesResponse"/>, <see cref="DynamicResponse"/> and <see cref="VoidResponse"/>
	/// </summary>
	public abstract class TransportResponse<T> : TransportResponse
	{
		/// <summary>
		/// The deserialized body returned by the product.
		/// </summary>
		public T Body { get; protected internal set; }
	}

	/// <summary>
	/// A response as returned by <see cref="ITransport{TConnectionSettings}"/> including details about the request/response life cycle.
	/// </summary>
	public abstract class TransportResponse
	{
		internal TransportResponse() { }

		/// <summary>
		/// 
		/// </summary>
		[JsonIgnore]
		public ApiCallDetails ApiCallDetails { get; internal set; }
	}
}
