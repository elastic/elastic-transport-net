// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Text;

namespace Elastic.Transport.Products.Elasticsearch;

/// <summary>
/// Extends the builtin responses with parsing for <see cref="ElasticsearchServerError"/>
/// </summary>
public static class ElasticsearchErrorExtensions
{
	/// <summary> Try to parse an Elasticsearch <see cref="ElasticsearchServerError"/> </summary>
	public static bool TryGetElasticsearchServerError(this StringResponse response, out ElasticsearchServerError serverError)
	{
		serverError = null;
		if (string.IsNullOrEmpty(response.Body) || response.ApiCallDetails.ResponseMimeType != RequestData.DefaultMimeType)
			return false;

		var settings = response.ApiCallDetails.TransportConfiguration;
		using var stream = settings.MemoryStreamFactory.Create(Encoding.UTF8.GetBytes(response.Body));
		return ElasticsearchServerError.TryCreate(stream, out serverError);
	}

	/// <summary> Try to parse an Elasticsearch <see cref="ElasticsearchServerError"/> </summary>
	public static bool TryGetElasticsearchServerError(this BytesResponse response, out ElasticsearchServerError serverError)
	{
		serverError = null;
		if (response.Body == null || response.Body.Length == 0 || response.ApiCallDetails.ResponseMimeType != RequestData.DefaultMimeType)
			return false;

		var settings = response.ApiCallDetails.TransportConfiguration;
		using var stream = settings.MemoryStreamFactory.Create(response.Body);
		return ElasticsearchServerError.TryCreate(stream, out serverError);
	}

	/// <summary>
	/// Try to parse an Elasticsearch <see cref="ElasticsearchServerError"/>, this only works if
	/// <see cref="ITransportConfiguration.DisableDirectStreaming"/> gives us access to <see cref="ApiCallDetails.RequestBodyInBytes"/>
	/// </summary>
	public static bool TryGetElasticsearchServerError(this TransportResponse response, out ElasticsearchServerError serverError)
	{
		serverError = null;
		var bytes = response.ApiCallDetails.ResponseBodyInBytes;
		if (bytes == null || response.ApiCallDetails.ResponseMimeType != RequestData.DefaultMimeType)
			return false;

		var settings = response.ApiCallDetails.TransportConfiguration;
		using var stream = settings.MemoryStreamFactory.Create(bytes);
		return ElasticsearchServerError.TryCreate(stream, out serverError);
	}
}
