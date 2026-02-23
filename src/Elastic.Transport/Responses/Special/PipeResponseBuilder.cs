// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

#if NET10_0_OR_GREATER
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Elastic.Transport;

internal class PipeResponseBuilder : TypedResponseBuilder<PipeResponse>
{
	protected override PipeResponse Build(ApiCallDetails apiCallDetails, BoundConfiguration boundConfiguration, Stream responseStream, string contentType, long contentLength) =>
		new(responseStream, contentType);

	protected override Task<PipeResponse> BuildAsync(ApiCallDetails apiCallDetails, BoundConfiguration boundConfiguration, Stream responseStream, string contentType,
		long contentLength, CancellationToken cancellationToken = default) =>
			Task.FromResult(new PipeResponse(responseStream, contentType));
}
#endif
