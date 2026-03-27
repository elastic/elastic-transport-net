// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Elastic.Transport.Products.Elasticsearch;

/// <summary>
/// A decorator that wraps any <see cref="IResponseBuilder"/> and adds Elasticsearch error extraction
/// for responses that implement <see cref="IElasticsearchResponse"/>.
/// <para>On error status codes (> 399), the decorator buffers the stream if needed, attempts to
/// deserialize an <see cref="ElasticsearchServerError"/>, resets the stream, then delegates to
/// the inner builder for body construction.</para>
/// </summary>
internal sealed class ElasticsearchErrorDecorator<T> : IResponseBuilder
	where T : TransportResponse, IElasticsearchResponse, new()
{
	private readonly IResponseBuilder _inner;

	public ElasticsearchErrorDecorator(IResponseBuilder inner) => _inner = inner;

	bool IResponseBuilder.CanBuild<TResponse>() => typeof(TResponse) == typeof(T);

	TResponse? IResponseBuilder.Build<TResponse>(ApiCallDetails apiCallDetails, BoundConfiguration boundConfiguration,
		Stream responseStream, string contentType, long contentLength)
		where TResponse : class =>
			BuildCoreAsync<TResponse>(false, apiCallDetails, boundConfiguration, responseStream, contentType, contentLength).EnsureCompleted();

	Task<TResponse?> IResponseBuilder.BuildAsync<TResponse>(ApiCallDetails apiCallDetails, BoundConfiguration boundConfiguration,
		Stream responseStream, string contentType, long contentLength, CancellationToken cancellationToken)
		where TResponse : class =>
			BuildCoreAsync<TResponse>(true, apiCallDetails, boundConfiguration, responseStream, contentType, contentLength, cancellationToken).AsTask();

	private async ValueTask<TResponse?> BuildCoreAsync<TResponse>(bool isAsync,
		ApiCallDetails details, BoundConfiguration boundConfiguration, Stream responseStream,
		string contentType, long contentLength, CancellationToken cancellationToken = default)
		where TResponse : TransportResponse, new()
	{
		ElasticsearchServerError? error = null;
		var ownsStream = false;

		if (details.HttpStatusCode > 399)
		{
			if (!responseStream.CanSeek)
			{
				var inMemoryStream = boundConfiguration.MemoryStreamFactory.Create();
				await responseStream.CopyToAsync(inMemoryStream, BufferedResponseHelpers.BufferSize, cancellationToken).ConfigureAwait(false);
				details.ResponseBodyInBytes = BufferedResponseHelpers.SwapStreams(ref responseStream, ref inMemoryStream);
				ownsStream = true;
			}

			ElasticsearchErrorHelper.TryGetError(boundConfiguration, responseStream, out error);
			responseStream.Position = 0;
		}

		T? response = isAsync
			? await _inner.BuildAsync<T>(details, boundConfiguration, responseStream, contentType, contentLength, cancellationToken).ConfigureAwait(false)
			: _inner.Build<T>(details, boundConfiguration, responseStream, contentType, contentLength);

		response ??= new T();

		if (error is not null)
			ElasticsearchResponseHelper.SetServerError(response, error);

		if (ownsStream && !response.LeaveOpen)
			responseStream.Dispose();

		return response as TResponse;
	}
}
