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

internal class DynamicResponseBuilder : TypedResponseBuilder<DynamicResponse>
{
	protected override DynamicResponse Build(ApiCallDetails apiCallDetails, RequestData requestData, Stream responseStream, string contentType, long contentLength) =>
		BuildCoreAsync(false, apiCallDetails, requestData, responseStream, contentType, contentLength).EnsureCompleted();

	protected override Task<DynamicResponse> BuildAsync(ApiCallDetails apiCallDetails, RequestData requestData, Stream responseStream, string contentType, long contentLength, CancellationToken cancellationToken = default) =>
		BuildCoreAsync(true, apiCallDetails, requestData, responseStream, contentType, contentLength, cancellationToken).AsTask();

	private static async ValueTask<DynamicResponse> BuildCoreAsync(bool isAsync, ApiCallDetails apiCallDetails, RequestData requestData, Stream responseStream,
		string contentType, long contentLength, CancellationToken cancellationToken = default)
	{
		DynamicResponse response;

		//if not json store the result under "body"
		if (contentType == null || !contentType.StartsWith(RequestData.DefaultContentType))
		{
			DynamicDictionary dictionary;
			string stringValue;

			if (apiCallDetails.ResponseBodyInBytes is not null)
			{
				stringValue = Encoding.UTF8.GetString(apiCallDetails.ResponseBodyInBytes);

				dictionary = new DynamicDictionary
				{
					["body"] = new DynamicValue(stringValue)
				};

				return new DynamicResponse(dictionary);
			}

#if NET8_0_OR_GREATER
			if (contentLength > -1 && contentLength <= 1_048_576)
			{
				var buffer = ArrayPool<byte>.Shared.Rent((int)contentLength);
				responseStream.ReadExactly(buffer, 0, (int)contentLength);
				stringValue = Encoding.UTF8.GetString(buffer.AsSpan(0, (int)contentLength));
				ArrayPool<byte>.Shared.Return(buffer);

				dictionary = new DynamicDictionary
				{
					["body"] = new DynamicValue(stringValue)
				};

				return new DynamicResponse(dictionary);
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

			dictionary = new DynamicDictionary
			{
				["body"] = new DynamicValue(stringValue)
			};

			response = new DynamicResponse(dictionary);
		}
		else
		{
			var body = LowLevelRequestResponseSerializer.Instance.Deserialize<DynamicDictionary>(responseStream);
			response = new DynamicResponse(body);
		}

		return response;
	}
}
