// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Elastic.Transport.Diagnostics;
using Elastic.Transport.Extensions;

using static Elastic.Transport.ResponseBuilderDefaults;

namespace Elastic.Transport;

internal static class ResponseBuilderDefaults
{
	public const int BufferSize = 81920;

	public static readonly Type[] SpecialTypes =
	{
		typeof(StringResponse), typeof(BytesResponse), typeof(VoidResponse), typeof(DynamicResponse), typeof(StreamResponse)
	};
}

/// <summary>
///     A helper class that deals with handling how a <see cref="Stream" /> is transformed to the requested
///     <see cref="TransportResponse" /> implementation. This includes handling optionally buffering based on
///     <see cref="ITransportConfiguration.DisableDirectStreaming" />. And handling short circuiting special responses
///     such as <see cref="StringResponse" />, <see cref="BytesResponse" /> and <see cref="VoidResponse" />
/// </summary>
internal class DefaultResponseBuilder<TError> : ResponseBuilder where TError : ErrorResponse, new()
{
	private readonly bool _isEmptyError;

	public DefaultResponseBuilder() => _isEmptyError = typeof(TError) == typeof(EmptyError);

	/// <summary>
	///     Create an instance of <typeparamref name="TResponse" /> from <paramref name="responseStream" />
	/// </summary>
	public override TResponse ToResponse<TResponse>(
		RequestData requestData,
		Exception ex,
		int? statusCode,
		Dictionary<string, IEnumerable<string>> headers,
		Stream responseStream,
		string mimeType,
		long contentLength,
		IReadOnlyDictionary<string, ThreadPoolStatistics> threadPoolStats,
		IReadOnlyDictionary<TcpState, int> tcpStats
	)
	{
		responseStream.ThrowIfNull(nameof(responseStream));

		var details = Initialize(requestData, ex, statusCode, headers, mimeType, threadPoolStats, tcpStats, contentLength);

		TResponse response = null;

		// Only attempt to set the body if the response may have content
		if (MayHaveBody(statusCode, requestData.Method, contentLength))
			response = SetBody<TResponse>(details, requestData, responseStream, mimeType);
		else
			responseStream.Dispose();

		response ??= new TResponse();

		response.ApiCallDetails = details;
		return response;
	}

	/// <summary>
	///     Create an instance of <typeparamref name="TResponse" /> from <paramref name="responseStream" />
	/// </summary>
	public override async Task<TResponse> ToResponseAsync<TResponse>(
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
	)
	{
		responseStream.ThrowIfNull(nameof(responseStream));

		var details = Initialize(requestData, ex, statusCode, headers, mimeType, threadPoolStats, tcpStats, contentLength);

		TResponse response = null;

		// Only attempt to set the body if the response may have content
		if (MayHaveBody(statusCode, requestData.Method, contentLength))
			response = await SetBodyAsync<TResponse>(details, requestData, responseStream, mimeType,
				cancellationToken).ConfigureAwait(false);
		else
			responseStream.Dispose();

		response ??= new TResponse();

		response.ApiCallDetails = details;
		return response;
	}

	// A helper which returns true if the response could potentially have a body.
	// We check for content-length != 0 rather than > 0 as we may not have a content-length header and the length may be -1.
	// In that case, we may have a body and can only use the status code and method conditions to rule out a potential body.
	private static bool MayHaveBody(int? statusCode, HttpMethod httpMethod, long contentLength) =>
		contentLength != 0 && (!statusCode.HasValue || statusCode.Value != 204 && httpMethod != HttpMethod.HEAD);

	private static ApiCallDetails Initialize(RequestData requestData, Exception exception, int? statusCode, Dictionary<string, IEnumerable<string>> headers, string mimeType, IReadOnlyDictionary<string,
		ThreadPoolStatistics> threadPoolStats, IReadOnlyDictionary<TcpState, int> tcpStats, long contentLength)
	{
		var hasSuccessfulStatusCode = false;
		var allowedStatusCodes = requestData.AllowedStatusCodes;
		if (statusCode.HasValue)
		{
			if (allowedStatusCodes.Contains(-1) || allowedStatusCodes.Contains(statusCode.Value))
				hasSuccessfulStatusCode = true;
			else
				hasSuccessfulStatusCode = requestData.ConnectionSettings
					.StatusCodeToResponseSuccess(requestData.Method, statusCode.Value);
		}

		// We don't validate the content-type (MIME type) for HEAD requests or responses that have no content (204 status code).
		// Elastic Cloud responses to HEAD requests strip the content-type header so we want to avoid validation in that case.
		var hasExpectedContentType = !MayHaveBody(statusCode, requestData.Method, contentLength) || requestData.ValidateResponseContentType(mimeType);

		var details = new ApiCallDetails
		{
			HasSuccessfulStatusCode = hasSuccessfulStatusCode,
			HasExpectedContentType = hasExpectedContentType,
			OriginalException = exception,
			HttpStatusCode = statusCode,
			RequestBodyInBytes = requestData.PostData?.WrittenBytes,
			Uri = requestData.Uri,
			HttpMethod = requestData.Method,
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
	///
	/// </summary>
	/// <param name="details"></param>
	/// <param name="requestData"></param>
	/// <returns></returns>
	protected virtual bool RequiresErrorDeserialization(ApiCallDetails details, RequestData requestData) => false;

	/// <summary>
	///
	/// </summary>
	/// <param name="apiCallDetails"></param>
	/// <param name="requestData"></param>
	/// <param name="responseStream"></param>
	/// <param name="error"></param>
	/// <returns></returns>
	protected virtual bool TryGetError(ApiCallDetails apiCallDetails, RequestData requestData, Stream responseStream, out TError? error)
	{
		if (!_isEmptyError)
		{
			error = null;
			return false;
		}

		error = EmptyError.Instance as TError;

		return error is not null;
	}

	/// <summary>
	///
	/// </summary>
	/// <typeparam name="TResponse"></typeparam>
	/// <param name="response"></param>
	/// <param name="error"></param>
	protected virtual void SetErrorOnResponse<TResponse>(TResponse response, TError error) where TResponse : TransportResponse, new() { }

	private TResponse SetBody<TResponse>(ApiCallDetails details, RequestData requestData,
		Stream responseStream, string mimeType)
		where TResponse : TransportResponse, new() =>
			SetBodyCoreAsync<TResponse>(false, details, requestData, responseStream, mimeType).EnsureCompleted();

	private Task<TResponse> SetBodyAsync<TResponse>(
		ApiCallDetails details, RequestData requestData, Stream responseStream, string mimeType,
		CancellationToken cancellationToken) where TResponse : TransportResponse, new() =>
			SetBodyCoreAsync<TResponse>(true, details, requestData, responseStream, mimeType, cancellationToken).AsTask();

	private async ValueTask<TResponse> SetBodyCoreAsync<TResponse>(bool isAsync,
		ApiCallDetails details, RequestData requestData, Stream responseStream, string mimeType,
		CancellationToken cancellationToken = default)
		where TResponse : TransportResponse, new()
	{
		byte[] bytes = null;
		var disableDirectStreaming = requestData.PostData?.DisableDirectStreaming ?? requestData.ConnectionSettings.DisableDirectStreaming;
		var requiresErrorDeserialization = RequiresErrorDeserialization(details, requestData);

		if (disableDirectStreaming || NeedsToEagerReadStream<TResponse>() || requiresErrorDeserialization)
		{
			var inMemoryStream = requestData.MemoryStreamFactory.Create();

			if (isAsync)
				await responseStream.CopyToAsync(inMemoryStream, BufferSize, cancellationToken).ConfigureAwait(false);
			else
				responseStream.CopyTo(inMemoryStream, BufferSize);

			bytes = SwapStreams(ref responseStream, ref inMemoryStream);
			details.ResponseBodyInBytes = bytes;
		}

		if (SetSpecialTypes<TResponse>(mimeType, bytes, responseStream, requestData.MemoryStreamFactory, out var r)) return r;

		if (details.HttpStatusCode.HasValue &&
			requestData.SkipDeserializationForStatusCodes.Contains(details.HttpStatusCode.Value))
			return null;

		var serializer = requestData.ConnectionSettings.RequestResponseSerializer;

		TResponse response;
		if (requestData.CustomResponseBuilder != null)
		{
			var beforeTicks = Stopwatch.GetTimestamp();

			if (isAsync)
				response = await requestData.CustomResponseBuilder
					.DeserializeResponseAsync(serializer, details, responseStream, cancellationToken)
					.ConfigureAwait(false) as TResponse;
			else
				response = requestData.CustomResponseBuilder
					.DeserializeResponse(serializer, details, responseStream) as TResponse;

			var deserializeResponseMs = (Stopwatch.GetTimestamp() - beforeTicks) / (Stopwatch.Frequency / 1000);
			if (deserializeResponseMs > OpenTelemetry.MinimumMillisecondsToEmitTimingSpanAttribute && OpenTelemetry.CurrentSpanIsElasticTransportOwnedHasListenersAndAllDataRequested)
				Activity.Current?.SetTag(OpenTelemetryAttributes.ElasticTransportDeserializeResponseMs, deserializeResponseMs);

			return response;
		}

		// TODO: Handle empty data in a nicer way as throwing exceptions has a cost we'd like to avoid!
		// ie. check content-length (add to ApiCallDetails)? Content-length cannot be retrieved from a GZip content stream which is annoying.
		try
		{
			if (requiresErrorDeserialization && TryGetError(details, requestData, responseStream, out var error) && error.HasError())
			{
				response = new TResponse();
				SetErrorOnResponse(response, error);
				return response;
			}

			if (!requestData.ValidateResponseContentType(mimeType))
				return default;

			var beforeTicks = Stopwatch.GetTimestamp();

			if (isAsync)
				response = await serializer.DeserializeAsync<TResponse>(responseStream, cancellationToken).ConfigureAwait(false);
			else
				response = serializer.Deserialize<TResponse>(responseStream);

			var deserializeResponseMs = (Stopwatch.GetTimestamp() - beforeTicks) / (Stopwatch.Frequency / 1000);

			if (deserializeResponseMs > OpenTelemetry.MinimumMillisecondsToEmitTimingSpanAttribute && OpenTelemetry.CurrentSpanIsElasticTransportOwnedHasListenersAndAllDataRequested)
				Activity.Current?.SetTag(OpenTelemetryAttributes.ElasticTransportDeserializeResponseMs, deserializeResponseMs);

			if (!response.LeaveOpen)
				responseStream.Dispose();

			return response;
		}
		catch (JsonException ex) when (ex.Message.Contains("The input does not contain any JSON tokens"))
		{
			responseStream.Dispose();
			return default;
		}
	}

	private static bool SetSpecialTypes<TResponse>(string mimeType, byte[] bytes, Stream responseStream,
		MemoryStreamFactory memoryStreamFactory, out TResponse cs)
		where TResponse : TransportResponse, new()
	{
		cs = null;
		var responseType = typeof(TResponse);
		if (!SpecialTypes.Contains(responseType)) return false;

		if (responseType == typeof(StringResponse))
			cs = new StringResponse(bytes.Utf8String()) as TResponse;
		else if (responseType == typeof(StreamResponse))
			cs = new StreamResponse(responseStream, mimeType) as TResponse;
		else if (responseType == typeof(BytesResponse))
			cs = new BytesResponse(bytes) as TResponse;
		else if (responseType == typeof(VoidResponse))
			cs = VoidResponse.Default as TResponse;
		else if (responseType == typeof(DynamicResponse))
		{
			//if not json store the result under "body"
			if (mimeType == null || !mimeType.StartsWith(RequestData.DefaultMimeType))
			{
				var dictionary = new DynamicDictionary
				{
					["body"] = new DynamicValue(bytes.Utf8String())
				};
				cs = new DynamicResponse(dictionary) as TResponse;
			}
			else
			{
				using var ms = memoryStreamFactory.Create(bytes);
				var body = LowLevelRequestResponseSerializer.Instance.Deserialize<DynamicDictionary>(ms);
				cs = new DynamicResponse(body) as TResponse;
			}
		}

		return cs != null;
	}

	private static bool NeedsToEagerReadStream<TResponse>()
		where TResponse : TransportResponse, new() =>
		typeof(TResponse) == typeof(StringResponse)
		|| typeof(TResponse) == typeof(BytesResponse)
		|| typeof(TResponse) == typeof(DynamicResponse);

	private static byte[] SwapStreams(ref Stream responseStream, ref MemoryStream ms)
	{
		var bytes = ms.ToArray();
		responseStream.Dispose();
		responseStream = ms;
		responseStream.Position = 0;
		return bytes;
	}
}
