// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Elastic.Transport;

internal class StreamResponseBuilder : TypedResponseBuilder<StreamResponse>
{
	protected override StreamResponse Build(ApiCallDetails apiCallDetails, BoundConfiguration boundConfiguration, Stream responseStream, string contentType, long contentLength) =>
		new(responseStream, contentType);

	protected override Task<StreamResponse> BuildAsync(ApiCallDetails apiCallDetails, BoundConfiguration boundConfiguration, Stream responseStream, string contentType,
		long contentLength, CancellationToken cancellationToken = default) =>
			Task.FromResult(new StreamResponse(responseStream, contentType));
}
