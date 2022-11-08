// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

namespace Elastic.Transport
{
	/// <summary>
	/// TODO
	/// </summary>
	public static class ResponseFactory
	{
		/// <summary>
		/// 
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <returns></returns>
		public static T CreateResponse<T>(T response, int statusCode) where T : TransportResponse =>
			CreateResponse<T>(response, statusCode, true);

		/// <summary>
		/// 
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="response"></param>
		/// <param name="statusCode"></param>
		/// <param name="success"></param>
		/// <returns></returns>
		public static T CreateResponse<T>(T response, int statusCode, bool success) where T : TransportResponse =>
			CreateResponse<T>(response, statusCode, HttpMethod.GET, success);

		/// <summary>
		/// 
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="response"></param>
		/// <param name="statusCode"></param>
		/// <param name="httpMethod"></param>
		/// <param name="success"></param>
		/// <returns></returns>
		public static T CreateResponse<T>(T response, int statusCode, HttpMethod httpMethod, bool success) where T : TransportResponse
		{
			var apiCallDetails = new ApiCallDetails { HttpStatusCode = statusCode, Success = success, HttpMethod = httpMethod };
			return CreateResponse<T>(response, apiCallDetails);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="response"></param>
		/// <param name="apiCallDetails"></param>
		/// <returns></returns>
		internal static T CreateResponse<T>(T response, ApiCallDetails apiCallDetails) where T : TransportResponse
		{
			response.ApiCallDetails = apiCallDetails;
			return response;
		}
	}
}
