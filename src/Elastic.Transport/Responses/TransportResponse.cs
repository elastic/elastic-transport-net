// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Text.Json.Serialization;
using Elastic.Transport.Diagnostics;
using Elastic.Transport.Diagnostics.Auditing;

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
	public abstract class TransportResponse : IApiCallDetails, ITransportResponse
	{
		/// <inheritdoc />
		[JsonIgnore]
		public IApiCallDetails ApiCall { get; set; }

		/// <inheritdoc cref="IApiCallDetails.TcpStats"/>
		[JsonIgnore]
		public IReadOnlyDictionary<TcpState, int> TcpStats => ApiCall.TcpStats;

		/// <inheritdoc cref="IApiCallDetails.DebugInformation"/>
		[JsonIgnore]
		public string DebugInformation => ApiCall.DebugInformation;

		/// <inheritdoc cref="IApiCallDetails.HttpMethod"/>
		[JsonIgnore]
		public HttpMethod HttpMethod => ApiCall.HttpMethod;

		/// <inheritdoc cref="IApiCallDetails.AuditTrail"/>
		[JsonIgnore]
		public IEnumerable<Audit> AuditTrail => ApiCall.AuditTrail;

		/// <inheritdoc cref="IApiCallDetails.ThreadPoolStats"/>
		[JsonIgnore]
		public IReadOnlyDictionary<string, ThreadPoolStatistics> ThreadPoolStats => ApiCall.ThreadPoolStats;

		/// <inheritdoc cref="IApiCallDetails.SuccessOrKnownError"/>
		[JsonIgnore]
		public bool SuccessOrKnownError => ApiCall.SuccessOrKnownError;
		/// <inheritdoc cref="IApiCallDetails.HttpStatusCode"/>
		[JsonIgnore]
		public int? HttpStatusCode => ApiCall.HttpStatusCode;

		/// <inheritdoc cref="IApiCallDetails.Success"/>
		[JsonIgnore]
		public bool Success => ApiCall.Success;
		/// <inheritdoc cref="IApiCallDetails.OriginalException"/>
		[JsonIgnore]
		public Exception OriginalException => ApiCall.OriginalException;
		/// <inheritdoc cref="IApiCallDetails.ResponseMimeType"/>
		[JsonIgnore]
		public string ResponseMimeType => ApiCall.ResponseMimeType;
		/// <inheritdoc cref="IApiCallDetails.Uri"/>
		[JsonIgnore]
		public Uri Uri => ApiCall.Uri;

		/// <inheritdoc cref="IApiCallDetails.TransportConfiguration"/>
		[JsonIgnore]
		public ITransportConfiguration TransportConfiguration => ApiCall.TransportConfiguration;

		/// <inheritdoc cref="IApiCallDetails.ResponseBodyInBytes"/>
		[JsonIgnore]
		public byte[] ResponseBodyInBytes => ApiCall.ResponseBodyInBytes;

		/// <inheritdoc cref="IApiCallDetails.RequestBodyInBytes"/>
		[JsonIgnore]
		public byte[] RequestBodyInBytes => ApiCall.RequestBodyInBytes;

		/// <inheritdoc cref="IApiCallDetails.ParsedHeaders"/>
		[JsonIgnore]
		public IReadOnlyDictionary<string, IEnumerable<string>> ParsedHeaders => ApiCall.ParsedHeaders;

		/// <inheritdoc cref="IApiCallDetails.DebugInformation"/>
		public override string ToString() => ApiCall.ToString();
	}

}
