// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace Elastic.Transport.Products.Elasticsearch
{
	internal sealed class ElasticsearchResponseBuilder : DefaultResponseBuilder<ServerError>
	{
		protected override void SetErrorOnResponse<TResponse>(TResponse response, ServerError error)
		{
			if (response is ElasticsearchResponse elasticResponse)
			{
				elasticResponse.ServerError = error;
			}
		}

		protected override bool TryGetError(ApiCallDetails apiCallDetails, RequestData requestData, Stream responseStream, out ServerError error)
		{
			error = null;
			
			Debug.Assert(responseStream.CanSeek);

			var serializer = requestData.ConnectionSettings.RequestResponseSerializer;

			try
			{
				error = serializer.Deserialize<ServerError>(responseStream);
				return error is not null;
			}
			catch (JsonException)
			{
				// Empty catch as we'll try the original response type if the error serialization fails
			}
			finally
			{
				responseStream.Position = 0;
			}

			return false;
		}

		protected sealed override bool RequiresErrorDeserialization(ApiCallDetails apiCallDetails, RequestData requestData) => apiCallDetails.HttpStatusCode > 399;
	}
}
