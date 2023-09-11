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

namespace Elastic.Transport;

/// <summary>
/// Builds a <see cref="TransportResponse"/> from the provided response data.
/// </summary>
public abstract class ResponseBuilder
{
	/// <summary> Exposes a default response builder to implementers without sharing more internal types to handle empty errors</summary>
	public static ResponseBuilder Default { get; } = new DefaultResponseBuilder<EmptyError>();

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

	) where TResponse : TransportResponse, new();

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
	) where TResponse : TransportResponse, new();
}
