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
		[typeof(VoidResponse)] = new VoidResponseBuilder()
	};

	/// <inheritdoc/>
	public override TResponse Create<TResponse>(
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
		Stream responseStream,
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
		Stream responseStream,
		string? contentType,
		long contentLength,
		IReadOnlyDictionary<string, ThreadPoolStatistics>? threadPoolStats,
		IReadOnlyDictionary<TcpState, int>? tcpStats,
		CancellationToken cancellationToken = default) where TResponse : TransportResponse, new()
	{
		var details = InitializeApiCallDetails(endpoint, boundConfiguration, postData, ex, statusCode, headers, contentType, threadPoolStats, tcpStats, contentLength);

		TResponse? response = null;

		if (responseStream is not null && MayHaveBody(statusCode, endpoint.Method, contentLength)
			&& TryResolveBuilder<TResponse>(boundConfiguration.ResponseBuilders, boundConfiguration.ProductResponseBuilders, out var builder))
		{
			var ownsStream = false;

			// We always pre-buffer when there may be a body, even if the content type does not match.
			// That way, we ensure the caller can access the bytes themselves for "invalid" responses.
			if (boundConfiguration.DisableDirectStreaming)
			{
				var inMemoryStream = boundConfiguration.MemoryStreamFactory.Create();

				if (isAsync)
					await responseStream.CopyToAsync(inMemoryStream, BufferedResponseHelpers.BufferSize, cancellationToken).ConfigureAwait(false);
				else
					responseStream.CopyTo(inMemoryStream, BufferedResponseHelpers.BufferSize);

				details.ResponseBodyInBytes = BufferedResponseHelpers.SwapStreams(ref responseStream, ref inMemoryStream);
				ownsStream = true;
			}

			// We only attempt to build a response when the Content-Type matches the accepted type.
			if (ValidateResponseContentType(boundConfiguration.Accept, contentType))
			{
				if (isAsync)
					response = await builder.BuildAsync<TResponse>(details, boundConfiguration, responseStream, contentType, contentLength, cancellationToken).ConfigureAwait(false);
				else
					response = builder.Build<TResponse>(details, boundConfiguration, responseStream, contentType, contentLength);
			}

			if (ownsStream && (response is null || !response.LeaveOpen))
				responseStream?.Dispose();
		}

		response ??= new TResponse();
		response.ApiCallDetails = details;
		return response;
	}

	private bool TryResolveBuilder<TResponse>(IReadOnlyCollection<IResponseBuilder> responseBuilders,
		IReadOnlyCollection<IResponseBuilder> productResponseBuilders, out IResponseBuilder builder
	) where TResponse : TransportResponse, new()
	{
		var type = typeof(TResponse);

		if (_resolvedBuilders.TryGetValue(type, out builder))
			return true;

		if (TryFindResponseBuilder(type,responseBuilders, _resolvedBuilders, ref builder))
			return true;

		return TryFindResponseBuilder(type, productResponseBuilders, _resolvedBuilders, ref builder);

		static bool TryFindResponseBuilder(Type type, IEnumerable<IResponseBuilder> responseBuilders, ConcurrentDictionary<Type, IResponseBuilder> resolvedBuilders, ref IResponseBuilder builder)
		{
			foreach (var potentialBuilder in responseBuilders)
			{
				if (potentialBuilder.CanBuild<TResponse>())
				{
					resolvedBuilders.TryAdd(type, potentialBuilder);
					builder = potentialBuilder;
					return true;
				}
			}

			return false;
		}
	}
}
