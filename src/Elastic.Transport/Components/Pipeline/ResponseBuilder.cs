// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
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
		Endpoint endpoint,
		RequestData requestData,
		PostData? postData,
		Exception? ex,
		int? statusCode,
		Dictionary<string, IEnumerable<string>>? headers,
		Stream responseStream,
		string? mimeType,
		long contentLength,
		IReadOnlyDictionary<string, ThreadPoolStatistics>? threadPoolStats,
		IReadOnlyDictionary<TcpState, int>? tcpStats

	) where TResponse : TransportResponse, new();

	/// <summary>
	/// Create an instance of <typeparamref name="TResponse" /> from <paramref name="responseStream" />
	/// </summary>
	public abstract Task<TResponse> ToResponseAsync<TResponse>(
		Endpoint endpoint,
		RequestData requestData,
		PostData? postData,
		Exception? ex,
		int? statusCode,
		Dictionary<string, IEnumerable<string>>? headers,
		Stream responseStream,
		string? mimeType,
		long contentLength,
		IReadOnlyDictionary<string, ThreadPoolStatistics>? threadPoolStats,
		IReadOnlyDictionary<TcpState, int>? tcpStats,
		CancellationToken cancellationToken = default
	) where TResponse : TransportResponse, new();

	internal static ApiCallDetails Initialize(
		Endpoint endpoint,
		RequestData requestData,
		PostData? postData,
		Exception exception,
		int? statusCode,
		Dictionary<string, IEnumerable<string>> headers, string mimeType,
		IReadOnlyDictionary<string,
		ThreadPoolStatistics> threadPoolStats, IReadOnlyDictionary<TcpState, int> tcpStats,
		long contentLength
	)
	{
		var hasSuccessfulStatusCode = false;
		var allowedStatusCodes = requestData.AllowedStatusCodes;
		if (statusCode.HasValue)
		{
			if (allowedStatusCodes.Contains(-1) || allowedStatusCodes.Contains(statusCode.Value))
				hasSuccessfulStatusCode = true;
			else
				hasSuccessfulStatusCode = requestData.ConnectionSettings
					.StatusCodeToResponseSuccess(endpoint.Method, statusCode.Value);
		}

		// We don't validate the content-type (MIME type) for HEAD requests or responses that have no content (204 status code).
		// Elastic Cloud responses to HEAD requests strip the content-type header so we want to avoid validation in that case.
		var hasExpectedContentType = !MayHaveBody(statusCode, endpoint.Method, contentLength) || requestData.ValidateResponseContentType(mimeType);

		var details = new ApiCallDetails
		{
			HasSuccessfulStatusCode = hasSuccessfulStatusCode,
			HasExpectedContentType = hasExpectedContentType,
			OriginalException = exception,
			HttpStatusCode = statusCode,
			RequestBodyInBytes = postData?.WrittenBytes,
			Uri = endpoint.Uri,
			HttpMethod = endpoint.Method,
			TcpStats = tcpStats,
			ThreadPoolStats = threadPoolStats,
			ResponseMimeType = mimeType,
			TransportConfiguration = requestData.ConnectionSettings
		};

		if (headers is not null)
			details.ParsedHeaders = new ReadOnlyDictionary<string, IEnumerable<string>>(headers);

		return details;
	}

	/// <summary>
	/// A helper which returns true if the response could potentially have a body.
	/// We check for content-length != 0 rather than > 0 as we may not have a content-length header and the length may be -1.
	/// In that case, we may have a body and can only use the status code and method conditions to rule out a potential body.
	/// </summary>
	protected static bool MayHaveBody(int? statusCode, HttpMethod httpMethod, long contentLength) =>
		contentLength != 0 && (!statusCode.HasValue || statusCode.Value != 204 && httpMethod != HttpMethod.HEAD);

}
