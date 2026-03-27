// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Elastic.Transport.Products.Elasticsearch;

/// <summary>
/// Builds <see cref="ElasticsearchStreamResponse"/> from a raw response stream.
/// </summary>
internal sealed class ElasticsearchStreamResponseBuilder : TypedResponseBuilder<ElasticsearchStreamResponse>
{
	protected override ElasticsearchStreamResponse Build(ApiCallDetails apiCallDetails, BoundConfiguration boundConfiguration,
		Stream responseStream, string contentType, long contentLength) =>
		new(responseStream, contentType);

	protected override Task<ElasticsearchStreamResponse> BuildAsync(ApiCallDetails apiCallDetails, BoundConfiguration boundConfiguration,
		Stream responseStream, string contentType, long contentLength, CancellationToken cancellationToken = default) =>
		Task.FromResult(new ElasticsearchStreamResponse(responseStream, contentType));
}

/// <summary>
/// Builds <see cref="ElasticsearchVoidResponse"/> without reading the response body.
/// </summary>
internal sealed class ElasticsearchVoidResponseBuilder : TypedResponseBuilder<ElasticsearchVoidResponse>
{
	protected override ElasticsearchVoidResponse Build(ApiCallDetails apiCallDetails, BoundConfiguration boundConfiguration,
		Stream responseStream, string contentType, long contentLength) => new();

	protected override Task<ElasticsearchVoidResponse> BuildAsync(ApiCallDetails apiCallDetails, BoundConfiguration boundConfiguration,
		Stream responseStream, string contentType, long contentLength, CancellationToken cancellationToken = default) =>
		Task.FromResult(new ElasticsearchVoidResponse());
}

#if NET10_0_OR_GREATER
/// <summary>
/// Builds <see cref="ElasticsearchPipeResponse"/> from a raw response stream.
/// </summary>
internal sealed class ElasticsearchPipeResponseBuilder : TypedResponseBuilder<ElasticsearchPipeResponse>
{
	protected override ElasticsearchPipeResponse Build(ApiCallDetails apiCallDetails, BoundConfiguration boundConfiguration,
		Stream responseStream, string contentType, long contentLength) =>
		new(responseStream, contentType);

	protected override Task<ElasticsearchPipeResponse> BuildAsync(ApiCallDetails apiCallDetails, BoundConfiguration boundConfiguration,
		Stream responseStream, string contentType, long contentLength, CancellationToken cancellationToken = default) =>
		Task.FromResult(new ElasticsearchPipeResponse(responseStream, contentType));
}
#endif
