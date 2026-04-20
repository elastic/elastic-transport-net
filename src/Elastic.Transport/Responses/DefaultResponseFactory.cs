// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;

using Elastic.Transport.Diagnostics;
using Elastic.Transport.Extensions;

namespace Elastic.Transport;

/// <summary>
/// A <see cref="ResponseFactory"/> which resolves an <see cref="IResponseBuilder"/> for each response being created.
/// </summary>
/// <remarks>
/// Create an instance of the factory using the provided configuration.
/// </remarks>
internal sealed class DefaultResponseFactory : ResponseFactory
{
	private readonly ConcurrentDictionary<Type, IResponseBuilder> _resolvedBuilders = new()
	{
		[typeof(BytesResponse)] = new BytesResponseBuilder(),
		[typeof(StreamResponse)] = new StreamResponseBuilder(),
		[typeof(StringResponse)] = new StringResponseBuilder(),
		[typeof(DynamicResponse)] = new DynamicResponseBuilder(),
		[typeof(JsonResponse)] = new JsonResponseBuilder(),
		[typeof(VoidResponse)] = new VoidResponseBuilder(),
#if NET10_0_OR_GREATER
		[typeof(PipeResponse)] = new PipeResponseBuilder(),
#endif
	};

	/// <inheritdoc/>
	public override TResponse Create<TResponse>(
		Endpoint endpoint,
		BoundConfiguration boundConfiguration,
		PostData? postData,
		Exception? ex,
		int? statusCode,
		Dictionary<string, IEnumerable<string>>? headers,
		Stream? responseStream,
		string? contentType,
		long contentLength,
		IReadOnlyDictionary<string, ThreadPoolStatistics>? threadPoolStats,
		IReadOnlyDictionary<TcpState, int>? tcpStats) =>
			CreateCoreAsync<TResponse>(false, endpoint, boundConfiguration, postData, ex, statusCode, headers, responseStream,
				contentType, contentLength, threadPoolStats, tcpStats).EnsureCompleted();

	/// <inheritdoc/>
	public override Task<TResponse> CreateAsync<TResponse>(
		Endpoint endpoint,
		BoundConfiguration boundConfiguration,
		PostData? postData,
		Exception? ex,
		int? statusCode,
		Dictionary<string, IEnumerable<string>>? headers,
		Stream? responseStream,
		string? contentType,
		long contentLength,
		IReadOnlyDictionary<string, ThreadPoolStatistics>? threadPoolStats,
		IReadOnlyDictionary<TcpState, int>? tcpStats,
		CancellationToken cancellationToken = default) =>
			CreateCoreAsync<TResponse>(true, endpoint, boundConfiguration, postData, ex, statusCode, headers, responseStream,
				contentType, contentLength, threadPoolStats, tcpStats, cancellationToken).AsTask();

	private async ValueTask<TResponse> CreateCoreAsync<TResponse>(
		bool isAsync,
		Endpoint endpoint,
		BoundConfiguration boundConfiguration,
		PostData? postData,
		Exception? ex,
		int? statusCode,
		Dictionary<string, IEnumerable<string>>? headers,
		Stream? responseStream,
		string? contentType,
		long contentLength,
		IReadOnlyDictionary<string, ThreadPoolStatistics>? threadPoolStats,
		IReadOnlyDictionary<TcpState, int>? tcpStats,
		CancellationToken cancellationToken = default) where TResponse : TransportResponse, new()
	{
		TResponse? response = null;
		var details = InitializeApiCallDetails(endpoint, boundConfiguration, postData, ex, statusCode, headers, contentType, threadPoolStats, tcpStats, contentLength);

		if (!MayHaveBody(statusCode, endpoint.Method, contentLength) || responseStream is null)
			return FinalizeResponse();

		var productRegistration = boundConfiguration.ConnectionSettings.ProductRegistration;

		var mayHaveErrorBody = statusCode.HasValue &&
			!productRegistration.HttpStatusCodeClassifier(endpoint.Method, statusCode.Value);

		var ownsStream = false;

		TryResolveBuilder<TResponse>(boundConfiguration.ResponseBuilders, boundConfiguration.ProductResponseBuilders, out var builder);

		// We always pre-buffer when there may be a body, even if the content type does not match.
		// That way, we ensure the caller can access the bytes themselves for "invalid" responses.
		if (boundConfiguration.DisableDirectStreaming)
		{
			responseStream = await BufferResponseStreamAsync(isAsync, boundConfiguration, details, responseStream, cancellationToken).ConfigureAwait(false);
			ownsStream = true;
		}

		// For non-success status codes, always buffer the response body so that callers
		// can inspect it via ApiCallDetails.ResponseBodyInBytes — even when the content-type
		// doesn't match the product's error format (e.g., HTML from a reverse proxy).
		if (mayHaveErrorBody)
		{
			if (!responseStream.CanSeek)
			{
				responseStream = await BufferResponseStreamAsync(isAsync, boundConfiguration, details, responseStream, cancellationToken).ConfigureAwait(false);
				ownsStream = true;
			}
			else if (details.ResponseBodyInBytes is null)
				await CaptureResponseBytesAsync(isAsync, boundConfiguration, details, responseStream, cancellationToken).ConfigureAwait(false);

			// Product-specific error extraction when the content-type matches.
			if (productRegistration.IsErrorContentType(contentType))
			{
				details.ProductError = productRegistration.TryExtractError(boundConfiguration, responseStream);
				responseStream.Position = 0;
			}
		}

		// We only attempt to build a response when the Content-Type matches the accepted type.
		if (builder is not null && ValidateResponseContentType(boundConfiguration.Accept, contentType) && contentType is not null)
		{
			response = isAsync
				? await builder.BuildAsync<TResponse>(details, boundConfiguration, responseStream, contentType, contentLength, cancellationToken).ConfigureAwait(false)
				: builder.Build<TResponse>(details, boundConfiguration, responseStream, contentType, contentLength);
		}

		if (ownsStream && (response is null || !response.LeaveOpen))
			responseStream.Dispose();

		return FinalizeResponse();

		TResponse FinalizeResponse()
		{
			response ??= new TResponse();
			response.ApiCallDetails = details;
			return response;
		}
	}

	/// <summary>
	/// Buffers the response stream into a seekable in-memory stream and records the bytes in
	/// <see cref="ApiCallDetails.ResponseBodyInBytes"/>. Returns the new in-memory stream.
	/// </summary>
	private static async ValueTask<Stream> BufferResponseStreamAsync(bool isAsync,
		BoundConfiguration boundConfiguration, ApiCallDetails details,
		Stream responseStream, CancellationToken cancellationToken)
	{
		var inMemoryStream = boundConfiguration.MemoryStreamFactory.Create();

		if (isAsync)
			await responseStream.CopyToAsync(inMemoryStream, BufferedResponseHelpers.BufferSize, cancellationToken).ConfigureAwait(false);
		else
			responseStream.CopyTo(inMemoryStream, BufferedResponseHelpers.BufferSize);

		details.ResponseBodyInBytes = BufferedResponseHelpers.SwapStreams(ref responseStream, ref inMemoryStream);
		return responseStream;
	}

	/// <summary>
	/// Captures the response bytes for diagnostics from a seekable stream without replacing it.
	/// The stream position is restored after reading.
	/// </summary>
	private static async ValueTask CaptureResponseBytesAsync(bool isAsync,
		BoundConfiguration boundConfiguration, ApiCallDetails details,
		Stream responseStream, CancellationToken cancellationToken)
	{
		var position = responseStream.Position;
		var inMemoryStream = boundConfiguration.MemoryStreamFactory.Create();

		if (isAsync)
			await responseStream.CopyToAsync(inMemoryStream, BufferedResponseHelpers.BufferSize, cancellationToken).ConfigureAwait(false);
		else
			responseStream.CopyTo(inMemoryStream, BufferedResponseHelpers.BufferSize);

		details.ResponseBodyInBytes = inMemoryStream.ToArray();
		responseStream.Position = position;
		inMemoryStream.Dispose();
	}

	private bool TryResolveBuilder<TResponse>(IReadOnlyCollection<IResponseBuilder> responseBuilders,
		IReadOnlyCollection<IResponseBuilder> productResponseBuilders, out IResponseBuilder? builder
	) where TResponse : TransportResponse, new()
	{
		var type = typeof(TResponse);

		if (_resolvedBuilders.TryGetValue(type, out var foundBuilder))
		{
			builder = foundBuilder;
			return true;
		}

		builder = null;
		if (TryFindResponseBuilder(type, responseBuilders, _resolvedBuilders, ref builder))
			return true;

		return TryFindResponseBuilder(type, productResponseBuilders, _resolvedBuilders, ref builder);

		static bool TryFindResponseBuilder(Type type, IEnumerable<IResponseBuilder> responseBuilders, ConcurrentDictionary<Type, IResponseBuilder> resolvedBuilders, ref IResponseBuilder? builder)
		{
			foreach (var potentialBuilder in responseBuilders)
			{
				if (!potentialBuilder.CanBuild<TResponse>())
					continue;

				_ = resolvedBuilders.TryAdd(type, potentialBuilder);
				builder = potentialBuilder;
				return true;
			}

			return false;
		}
	}
}
