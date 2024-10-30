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
///     <see cref="IRequestConfiguration.DisableDirectStreaming" />. And handling short circuiting special responses
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
		Endpoint endpoint,
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

		var details = Initialize(endpoint, requestData, ex, statusCode, headers, mimeType, threadPoolStats, tcpStats, contentLength);

		TResponse response = null;

		// Only attempt to set the body if the response may have content
		if (MayHaveBody(statusCode, requestData.Method, contentLength))
			response = SetBody<TResponse>(details, requestData, responseStream, mimeType);

		response ??= new TResponse();
		response.ApiCallDetails = details;
		return response;
	}

	/// <summary>
	///     Create an instance of <typeparamref name="TResponse" /> from <paramref name="responseStream" />
	/// </summary>
	public override async Task<TResponse> ToResponseAsync<TResponse>(
		Endpoint endpoint,
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

		var details = Initialize(endpoint, requestData, ex, statusCode, headers, mimeType, threadPoolStats, tcpStats, contentLength);

		TResponse response = null;

		// Only attempt to set the body if the response may have content
		if (MayHaveBody(statusCode, requestData.Method, contentLength))
			response = await SetBodyAsync<TResponse>(details, requestData, responseStream, mimeType,
				cancellationToken).ConfigureAwait(false);

		response ??= new TResponse();
		response.ApiCallDetails = details;
		return response;
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
		var disableDirectStreaming = requestData.DisableDirectStreaming;
		var requiresErrorDeserialization = RequiresErrorDeserialization(details, requestData);

		var ownsStream = false;

		if (disableDirectStreaming || NeedsToEagerReadStream<TResponse>() || requiresErrorDeserialization)
		{
			var inMemoryStream = requestData.MemoryStreamFactory.Create();

			if (isAsync)
				await responseStream.CopyToAsync(inMemoryStream, BufferSize, cancellationToken).ConfigureAwait(false);
			else
				responseStream.CopyTo(inMemoryStream, BufferSize);

			bytes = SwapStreams(ref responseStream, ref inMemoryStream);
			ownsStream = true;
			details.ResponseBodyInBytes = bytes;
		}

		if (TrySetSpecialType<TResponse>(mimeType, bytes, responseStream, requestData.MemoryStreamFactory, out var response))
		{
			ConditionalDisposal(responseStream, ownsStream, response);
			return response;
		}

		if (details.HttpStatusCode.HasValue &&
			requestData.SkipDeserializationForStatusCodes.Contains(details.HttpStatusCode.Value))
		{
			ConditionalDisposal(responseStream, ownsStream, response);
			return null;
		}

		var serializer = requestData.ConnectionSettings.RequestResponseSerializer;

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

			ConditionalDisposal(responseStream, ownsStream, response);
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
				ConditionalDisposal(responseStream, ownsStream, response);
				return response;
			}

			if (!requestData.ValidateResponseContentType(mimeType))
			{
				ConditionalDisposal(responseStream, ownsStream, response);
				return default;
			}

			var beforeTicks = Stopwatch.GetTimestamp();

			if (isAsync)
				response = await serializer.DeserializeAsync<TResponse>(responseStream, cancellationToken).ConfigureAwait(false);
			else
				response = serializer.Deserialize<TResponse>(responseStream);

			var deserializeResponseMs = (Stopwatch.GetTimestamp() - beforeTicks) / (Stopwatch.Frequency / 1000);

			if (deserializeResponseMs > OpenTelemetry.MinimumMillisecondsToEmitTimingSpanAttribute && OpenTelemetry.CurrentSpanIsElasticTransportOwnedHasListenersAndAllDataRequested)
				Activity.Current?.SetTag(OpenTelemetryAttributes.ElasticTransportDeserializeResponseMs, deserializeResponseMs);

			ConditionalDisposal(responseStream, ownsStream, response);
			return response;
		}
		catch (JsonException ex) when (ex.Message.Contains("The input does not contain any JSON tokens"))
		{
			// Note the exception this handles is ONLY thrown after a check if the stream length is zero.
			// When the length is zero, `default` is returned by Deserialize(Async) instead.
			ConditionalDisposal(responseStream, ownsStream, response);
			return default;
		}

		static void ConditionalDisposal(Stream responseStream, bool ownsStream, TResponse response)
		{
			// We only dispose of the responseStream if we created it (i.e. it is a MemoryStream) we
			// created via MemoryStreamFactory.
			if (ownsStream && (response is null || !response.LeaveOpen))
				responseStream.Dispose();
		}
	}

	private static bool TrySetSpecialType<TResponse>(string mimeType, byte[] bytes, Stream responseStream,
		MemoryStreamFactory memoryStreamFactory, out TResponse response)
		where TResponse : TransportResponse, new()
	{
		response = null;
		var responseType = typeof(TResponse);
		if (!SpecialTypes.Contains(responseType)) return false;

		if (responseType == typeof(StringResponse))
			response = new StringResponse(bytes.Utf8String()) as TResponse;
		else if (responseType == typeof(StreamResponse))
			response = new StreamResponse(responseStream, mimeType) as TResponse;
		else if (responseType == typeof(BytesResponse))
			response = new BytesResponse(bytes) as TResponse;
		else if (responseType == typeof(VoidResponse))
			response = VoidResponse.Default as TResponse;
		else if (responseType == typeof(DynamicResponse))
		{
			//if not json store the result under "body"
			if (mimeType == null || !mimeType.StartsWith(RequestData.DefaultMimeType))
			{
				var dictionary = new DynamicDictionary
				{
					["body"] = new DynamicValue(bytes.Utf8String())
				};
				response = new DynamicResponse(dictionary) as TResponse;
			}
			else
			{
				using var ms = memoryStreamFactory.Create(bytes);
				var body = LowLevelRequestResponseSerializer.Instance.Deserialize<DynamicDictionary>(ms);
				response = new DynamicResponse(body) as TResponse;
			}
		}

		return response != null;
	}

	private static bool NeedsToEagerReadStream<TResponse>()
		where TResponse : TransportResponse, new() =>
		typeof(TResponse) == typeof(StringResponse)
		|| typeof(TResponse) == typeof(BytesResponse)
		|| typeof(TResponse) == typeof(DynamicResponse);

	private static byte[] SwapStreams(ref Stream responseStream, ref MemoryStream ms)
	{
		var bytes = ms.ToArray();
		responseStream = ms;
		responseStream.Position = 0;
		return bytes;
	}
}
