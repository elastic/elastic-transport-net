// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Elastic.Transport;

internal class BytesResponseBuilder : TypedResponseBuilder<BytesResponse>
{
	protected override BytesResponse Build(ApiCallDetails apiCallDetails, BoundConfiguration boundConfiguration, Stream responseStream, string contentType, long contentLength) =>
		BuildCoreAsync(false, apiCallDetails, boundConfiguration, responseStream).EnsureCompleted();

	protected override Task<BytesResponse> BuildAsync(ApiCallDetails apiCallDetails, BoundConfiguration boundConfiguration, Stream responseStream, string contentType, long contentLength, CancellationToken cancellationToken = default) =>
		BuildCoreAsync(true, apiCallDetails, boundConfiguration, responseStream, cancellationToken).AsTask();

	private static async ValueTask<BytesResponse> BuildCoreAsync(bool isAsync, ApiCallDetails apiCallDetails, BoundConfiguration boundConfiguration, Stream responseStream, CancellationToken cancellationToken = default)
	{
		BytesResponse response;

		if (apiCallDetails.ResponseBodyInBytes is not null)
		{
			response = new BytesResponse(apiCallDetails.ResponseBodyInBytes);
			return response;
		}

		var tempStream = boundConfiguration.MemoryStreamFactory.Create();
		await responseStream.CopyToAsync(tempStream, BufferedResponseHelpers.BufferSize, cancellationToken).ConfigureAwait(false);
		apiCallDetails.ResponseBodyInBytes = BufferedResponseHelpers.SwapStreams(ref responseStream, ref tempStream);
		response = new BytesResponse(apiCallDetails.ResponseBodyInBytes);

#if NET6_0_OR_GREATER
		await responseStream.DisposeAsync().ConfigureAwait(false);
#else
		responseStream.Dispose();
#endif

		return response;
	}
}
