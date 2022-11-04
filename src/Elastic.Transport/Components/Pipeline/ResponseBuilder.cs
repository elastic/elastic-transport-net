// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using Elastic.Transport.Diagnostics;

namespace Elastic.Transport
{
	/// <summary>
	/// Builds a <see cref="ITransportResponse"/> from the provided response data.
	/// </summary>
	public abstract class ResponseBuilder
	{
		/// <summary>
		/// Create an instance of <typeparamref name="TResponse" /> from <paramref name="responseStream" />
		/// </summary>
		public abstract TResponse ToResponse<TResponse>(
			RequestData requestData,
			Exception ex,
			int? statusCode,
			Dictionary<string, IEnumerable<string>> headers,
			Stream responseStream,
			string mimeType,
			long contentLength,
			IReadOnlyDictionary<string, ThreadPoolStatistics> threadPoolStats,
			IReadOnlyDictionary<TcpState, int> tcpStats

		) where TResponse : class, ITransportResponse, new();

		/// <summary>
		/// Create an instance of <typeparamref name="TResponse" /> from <paramref name="responseStream" />
		/// </summary>
		public abstract Task<TResponse> ToResponseAsync<TResponse>(
			RequestData requestData,
			Exception ex,
			int? statusCode,
			Dictionary<string, IEnumerable<string>> headers,
			Stream responseStream,
			string mimeType,
			long contentLength,
			IReadOnlyDictionary<string, ThreadPoolStatistics> threadPoolStats,
			IReadOnlyDictionary<TcpState, int> tcpStats,
			CancellationToken cancellationToken = default
		) where TResponse : class, ITransportResponse, new();
	}
}
