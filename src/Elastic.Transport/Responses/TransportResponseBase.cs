/*
 * Licensed to Elasticsearch B.V. under one or more contributor
 * license agreements. See the NOTICE file distributed with
 * this work for additional information regarding copyright
 * ownership. Elasticsearch B.V. licenses this file to you under
 * the Apache License, Version 2.0 (the "License"); you may
 * not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *    http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing,
 * software distributed under the License is distributed on an
 * "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
 * KIND, either express or implied.  See the License for the
 * specific language governing permissions and limitations
 * under the License.
 */

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
	public abstract class TransportResponseBase<T> : TransportResponseBase
	{
		/// <summary> The deserialized body return by the product </summary>
		public T Body { get; protected internal set; }
	}

	/// <summary>
	/// A response as returned by <see cref="ITransport{TConnectionSettings}"/> including details about the request/response life cycle
	/// </summary>
	public abstract class TransportResponseBase : IApiCallDetails, ITransportResponse
	{

		/// <inheritdoc />
		[JsonIgnore]
		public IApiCallDetails ApiCall { get; set; }


		/// <inheritdoc cref="IApiCallDetails.TcpStats"/>
		[JsonIgnore]
		public ReadOnlyDictionary<TcpState, int> TcpStats
		{
			get => ApiCall.TcpStats;
			set => ApiCall.TcpStats = value;
		}

		/// <inheritdoc cref="IApiCallDetails.DebugInformation"/>
		[JsonIgnore]
		public string DebugInformation => ApiCall.DebugInformation;
		/// <inheritdoc cref="IApiCallDetails.HttpMethod"/>
		[JsonIgnore]
		public HttpMethod HttpMethod => ApiCall.HttpMethod;
		/// <inheritdoc cref="IApiCallDetails.AuditTrail"/>
		[JsonIgnore]
		public List<Audit> AuditTrail
		{
			get => ApiCall.AuditTrail;
			set => ApiCall.AuditTrail = value;
		}

		/// <inheritdoc cref="IApiCallDetails.ThreadPoolStats"/>
		[JsonIgnore]
		public ReadOnlyDictionary<string, ThreadPoolStatistics> ThreadPoolStats
		{
			get => ApiCall.ThreadPoolStats;
			set => ApiCall.ThreadPoolStats = value;
		}

		/// <inheritdoc cref="IApiCallDetails.DeprecationWarnings"/>
		[JsonIgnore]
		public IEnumerable<string> DeprecationWarnings => ApiCall.DeprecationWarnings;
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

		/// <inheritdoc cref="IApiCallDetails.ConnectionConfiguration"/>
		[JsonIgnore]
		public ITransportConfiguration ConnectionConfiguration => ApiCall.ConnectionConfiguration;

		/// <inheritdoc cref="IApiCallDetails.ResponseBodyInBytes"/>
		[JsonIgnore]
		public byte[] ResponseBodyInBytes => ApiCall.ResponseBodyInBytes;

		/// <inheritdoc cref="IApiCallDetails.RequestBodyInBytes"/>
		[JsonIgnore]
		public byte[] RequestBodyInBytes => ApiCall.RequestBodyInBytes;

		/// <inheritdoc cref="IApiCallDetails.DebugInformation"/>
		public override string ToString() => ApiCall.ToString();
	}

}
