/*
 * Licensed to Elasticsearch B.V. under one or more contributor
 * license agreements. See the NOTICE file distributed with
 * this work for additional information regarding copyright
 * ownership. Elasticsearch B.V. licenses this file to you under
 * the Apache License, Version 2.0 (the "License"); you may
 * not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *    http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing,
 * software distributed under the License is distributed on an
 * "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
 * KIND, either express or implied.  See the License for the
 * specific language governing permissions and limitations
 * under the License.
 */

using System.Text;
using Elastic.Transport.Products.Elasticsearch.Failures;

namespace Elastic.Transport.Products.Elasticsearch
{
	/// <summary>
	/// Extends the builtin responses with parsing for <see cref="ServerError"/>
	/// </summary>
	public static class ElasticsearchErrorExtensions
	{
		/// <summary> Try to parse an Elasticsearch <see cref="ServerError"/> </summary>
		public static bool TryGetElasticsearchServerError(this StringResponse response, out ServerError serverError)
		{
			serverError = null;
			if (string.IsNullOrEmpty(response.Body) || response.ResponseMimeType != RequestData.MimeType)
				return false;

			var settings = response.ApiCall.ConnectionConfiguration;
			using var stream = settings.MemoryStreamFactory.Create(Encoding.UTF8.GetBytes(response.Body));
			return ServerError.TryCreate(stream, out serverError);
		}

		/// <summary> Try to parse an Elasticsearch <see cref="ServerError"/> </summary>
		public static bool TryGetElasticsearchServerError(this BytesResponse response, out ServerError serverError)
		{
			serverError = null;
			if (response.Body == null || response.Body.Length == 0 || response.ResponseMimeType != RequestData.MimeType)
				return false;

			var settings = response.ApiCall.ConnectionConfiguration;
			using var stream = settings.MemoryStreamFactory.Create(response.Body);
			return ServerError.TryCreate(stream, out serverError);
		}

		/// <summary>
		/// Try to parse an Elasticsearch <see cref="ServerError"/>, this only works if
		/// <see cref="ITransportConfiguration.DisableDirectStreaming"/> gives us access to <see cref="IApiCallDetails.RequestBodyInBytes"/>
		/// </summary>
		public static bool TryGetElasticsearchServerError(this ITransportResponse response, out ServerError serverError)
		{
			serverError = null;
			var bytes = response.ApiCall.ResponseBodyInBytes;
			if (bytes == null || response.ApiCall.ResponseMimeType != RequestData.MimeType)
				return false;

			var settings = response.ApiCall.ConnectionConfiguration;
			using var stream = settings.MemoryStreamFactory.Create(bytes);
			return ServerError.TryCreate(stream, out serverError);
		}
	}
}
