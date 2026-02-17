// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

#if NET8_0_OR_GREATER
using System;
using System.Buffers;
#endif

using System.IO;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace Elastic.Transport;

internal class JsonResponseBuilder : TypedResponseBuilder<JsonResponse>
{
	protected override JsonResponse Build(ApiCallDetails apiCallDetails, BoundConfiguration boundConfiguration, Stream responseStream, string contentType, long contentLength) =>
		BuildCoreAsync(false, apiCallDetails, boundConfiguration, responseStream, contentType, contentLength).EnsureCompleted();

	protected override Task<JsonResponse> BuildAsync(ApiCallDetails apiCallDetails, BoundConfiguration boundConfiguration, Stream responseStream, string contentType, long contentLength, CancellationToken cancellationToken = default) =>
		BuildCoreAsync(true, apiCallDetails, boundConfiguration, responseStream, contentType, contentLength, cancellationToken).AsTask();

	private static async ValueTask<JsonResponse> BuildCoreAsync(bool isAsync, ApiCallDetails apiCallDetails, BoundConfiguration boundConfiguration, Stream responseStream,
		string contentType, long contentLength, CancellationToken cancellationToken = default)
	{
		// If not JSON, store the result under "body"
		if (contentType == null || !contentType.StartsWith(BoundConfiguration.DefaultContentType))
		{
			string stringValue;

			if (apiCallDetails.ResponseBodyInBytes is not null)
			{
				stringValue = Encoding.UTF8.GetString(apiCallDetails.ResponseBodyInBytes);
				return new JsonResponse(new JsonObject { ["body"] = stringValue });
			}

#if NET8_0_OR_GREATER
			if (contentLength > -1 && contentLength <= 1_048_576)
			{
				var buffer = ArrayPool<byte>.Shared.Rent((int)contentLength);
				responseStream.ReadExactly(buffer, 0, (int)contentLength);
				stringValue = Encoding.UTF8.GetString(buffer.AsSpan(0, (int)contentLength));
				ArrayPool<byte>.Shared.Return(buffer);
				return new JsonResponse(new JsonObject { ["body"] = stringValue });
			}
#endif

			var sr = new StreamReader(responseStream);

			if (isAsync)
			{
				stringValue = await sr.ReadToEndAsync
				(
#if NET8_0_OR_GREATER
					cancellationToken
#endif
				).ConfigureAwait(false);
			}
			else
			{
				stringValue = sr.ReadToEnd();
			}

			return new JsonResponse(new JsonObject { ["body"] = stringValue });
		}

		// JSON content: parse into JsonNode
		JsonNode node;
		if (isAsync)
		{
			node = await JsonNode.ParseAsync(responseStream, cancellationToken: cancellationToken).ConfigureAwait(false);
		}
		else
		{
			node = JsonNode.Parse(responseStream);
		}

		return new JsonResponse(node);
	}
}
