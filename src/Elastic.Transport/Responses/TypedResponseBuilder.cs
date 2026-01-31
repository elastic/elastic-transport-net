// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Elastic.Transport;

/// <summary>
/// A builder for a specific <typeparamref name="TResponse"/> type.
/// </summary>
/// <typeparam name="TResponse"></typeparam>
public abstract class TypedResponseBuilder<TResponse> : IResponseBuilder
{
	bool IResponseBuilder.CanBuild<T>() => typeof(TResponse) == typeof(T);

	/// <inheritdoc cref="IResponseBuilder.Build{TResponse}(ApiCallDetails, BoundConfiguration, Stream, string, long)"/>
	protected abstract TResponse Build(ApiCallDetails apiCallDetails, BoundConfiguration boundConfiguration, Stream responseStream, string contentType, long contentLength);

	T? IResponseBuilder.Build<T>(ApiCallDetails apiCallDetails, BoundConfiguration boundConfiguration, Stream responseStream, string contentType, long contentLength)
		where T : class =>
		Build(apiCallDetails, boundConfiguration, responseStream, contentType, contentLength) as T;

	/// <inheritdoc cref="IResponseBuilder.BuildAsync{TResponse}(ApiCallDetails, BoundConfiguration, Stream, string, long, CancellationToken)"/>
	protected abstract Task<TResponse> BuildAsync(ApiCallDetails apiCallDetails, BoundConfiguration boundConfiguration, Stream responseStream,
		string contentType, long contentLength, CancellationToken cancellationToken = default);

	async Task<T?> IResponseBuilder.BuildAsync<T>(ApiCallDetails apiCallDetails, BoundConfiguration boundConfiguration, Stream responseStream, string contentType,
		long contentLength, CancellationToken cancellationToken)
		where T : class =>
			await BuildAsync(apiCallDetails, boundConfiguration, responseStream, contentType, contentLength, cancellationToken).ConfigureAwait(false) as T;
}
