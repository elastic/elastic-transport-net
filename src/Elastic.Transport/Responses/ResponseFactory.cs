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
public abstract class ResponseFactory
{
	/// <summary>
	/// Create an instance of <typeparamref name="TResponse" /> from <paramref name="responseStream" />
	/// </summary>
	public abstract TResponse Create<TResponse>(
		Endpoint endpoint,
		BoundConfiguration boundConfiguration,
		PostData? postData,
		Exception? ex,
		int? statusCode,
		Dictionary<string, IEnumerable<string>>? headers,
		Stream responseStream,
		string? contentType,
		long contentLength,
		IReadOnlyDictionary<string, ThreadPoolStatistics>? threadPoolStats,
		IReadOnlyDictionary<TcpState, int>? tcpStats
	) where TResponse : TransportResponse, new();

	/// <summary>
	/// Create an instance of <typeparamref name="TResponse" /> from <paramref name="responseStream" />
	/// </summary>
	public abstract Task<TResponse> CreateAsync<TResponse>(
		Endpoint endpoint,
		BoundConfiguration boundConfiguration,
		PostData? postData,
		Exception? ex,
		int? statusCode,
		Dictionary<string, IEnumerable<string>>? headers,
		Stream responseStream,
		string? contentType,
		long contentLength,
		IReadOnlyDictionary<string, ThreadPoolStatistics>? threadPoolStats,
		IReadOnlyDictionary<TcpState, int>? tcpStats,
		CancellationToken cancellationToken = default
	) where TResponse : TransportResponse, new();

	internal static ApiCallDetails InitializeApiCallDetails(
		Endpoint endpoint,
		BoundConfiguration boundConfiguration,
		PostData? postData,
		Exception? exception,
		int? statusCode,
		Dictionary<string, IEnumerable<string>>? headers,
		string? contentType,
		IReadOnlyDictionary<string,
		ThreadPoolStatistics>? threadPoolStats,
		IReadOnlyDictionary<TcpState, int>? tcpStats,
		long contentLength)
	{
		var hasSuccessfulStatusCode = false;
		var allowedStatusCodes = boundConfiguration.AllowedStatusCodes;
		if (statusCode.HasValue)
		{
			if (allowedStatusCodes.Contains(-1) || allowedStatusCodes.Contains(statusCode.Value))
				hasSuccessfulStatusCode = true;
			else
				hasSuccessfulStatusCode = boundConfiguration.ConnectionSettings
					.StatusCodeToResponseSuccess(endpoint.Method, statusCode.Value);
		}

		// We don't validate the content-type (MIME type) for HEAD requests or responses that have no content (204 status code).
		// Elastic Cloud responses to HEAD requests strip the content-type header so we want to avoid validation in that case.
		var hasExpectedContentType = !MayHaveBody(statusCode, endpoint.Method, contentLength) || ValidateResponseContentType(boundConfiguration.Accept, contentType);

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
			ResponseContentType = contentType ?? string.Empty,
			TransportConfiguration = boundConfiguration.ConnectionSettings
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

	internal static bool ValidateResponseContentType(string accept, string? responseContentType)
	{
		if (string.IsNullOrEmpty(responseContentType)) return false;

		if (accept == responseContentType)
			return true;

		// TODO - Performance: Review options to avoid the replace here and compare more efficiently.
		// At this point, responseContentType is guaranteed to be non-null due to the check at line 116
		var trimmedAccept = accept.Replace(" ", "");
		var normalizedResponseContentType = responseContentType!.Replace(" ", "");

		return normalizedResponseContentType.Equals(trimmedAccept, StringComparison.OrdinalIgnoreCase)
			|| normalizedResponseContentType.StartsWith(trimmedAccept, StringComparison.OrdinalIgnoreCase)

			// ES specific fallback required because:
			// - 404 responses from ES8 don't include the vendored header
			// - ES8 EQL responses don't include vendored type

			|| trimmedAccept.Contains("application/vnd.elasticsearch+json")
			&& normalizedResponseContentType.StartsWith(BoundConfiguration.DefaultContentType, StringComparison.OrdinalIgnoreCase);
	}
}
