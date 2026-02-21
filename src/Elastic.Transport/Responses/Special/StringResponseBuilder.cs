// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

#if NET8_0_OR_GREATER
using System;
using System.Buffers;
#endif

using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Elastic.Transport;

internal class StringResponseBuilder : TypedResponseBuilder<StringResponse>
{
	protected override StringResponse Build(ApiCallDetails apiCallDetails, BoundConfiguration boundConfiguration, Stream responseStream, string contentType, long contentLength)
	{
		string responseString;

		if (apiCallDetails.ResponseBodyInBytes is not null)
		{
			responseString = Encoding.UTF8.GetString(apiCallDetails.ResponseBodyInBytes);
			return new StringResponse(responseString);
		}

#if NET8_0_OR_GREATER
		if (contentLength is > (-1) and <= 1_048_576)
		{
			var buffer = ArrayPool<byte>.Shared.Rent((int)contentLength);
			responseStream.ReadExactly(buffer, 0, (int)contentLength);
			responseString = Encoding.UTF8.GetString(buffer.AsSpan(0, (int)contentLength));
			ArrayPool<byte>.Shared.Return(buffer);
			return new StringResponse(responseString);
		}
#endif

		var sr = new StreamReader(responseStream);
		responseString = sr.ReadToEnd();
		return new StringResponse(responseString);
	}

	protected override async Task<StringResponse> BuildAsync(ApiCallDetails apiCallDetails, BoundConfiguration boundConfiguration, Stream responseStream, string contentType, long contentLength,
		CancellationToken cancellationToken = default)
	{
		string responseString;

		if (apiCallDetails.ResponseBodyInBytes is not null)
		{
			responseString = Encoding.UTF8.GetString(apiCallDetails.ResponseBodyInBytes);
			return new StringResponse(responseString);
		}

#if NET8_0_OR_GREATER
		if (contentLength is > (-1) and < 1_048_576)
		{
			var buffer = ArrayPool<byte>.Shared.Rent((int)contentLength);
			await responseStream.ReadExactlyAsync(buffer, 0, (int)contentLength, cancellationToken).ConfigureAwait(false);
			responseString = Encoding.UTF8.GetString(buffer.AsSpan(0, (int)contentLength));
			ArrayPool<byte>.Shared.Return(buffer);
			return new StringResponse(responseString);
		}
#endif

		var sr = new StreamReader(responseStream);
#if NET8_0_OR_GREATER
		responseString = await sr.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
#else
		responseString = await sr.ReadToEndAsync().ConfigureAwait(false);
#endif
		return new StringResponse(responseString);
	}
}
