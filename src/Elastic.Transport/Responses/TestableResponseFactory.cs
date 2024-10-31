// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

namespace Elastic.Transport;

/// <summary>
/// A factory used to configure responses that can be used for unit testing.
/// </summary>
public static class TestableResponseFactory
{
	/// <summary>
	/// Creates a response suitable for testing which includes the provided HTTP status code and is marked as successful.
	/// </summary>
	/// <param name="response">The <see cref="TransportResponse"/> to configure.</param>
	/// <param name="httpStatusCode">The HTTP status code to set on the final response.</param>
	/// <typeparam name="T">The response derived from <see cref="TransportResponse"/> that will be returned.</typeparam>
	/// <returns>The <typeparamref name="T"/> response configured as a successful server response with the provided status code.</returns>
	public static T CreateSuccessfulResponse<T>(T response, int httpStatusCode) where T : TransportResponse
		=> CreateResponse(response, httpStatusCode, true);

	/// <summary>
	/// Creates a response suitable for testing which includes the provided HTTP status code and is marked as successful.
	/// </summary>
	/// <typeparam name="T">The response derived from <see cref="TransportResponse"/> that will be returned.</typeparam>
	/// <param name="response">The <see cref="TransportResponse"/> to configure.</param>
	/// <param name="httpStatusCode">The HTTP status code to set on the final response.</param>
	/// <param name="statusCodeRepresentsSuccess">Control whether the provided HTTP status code should be considered a success for this response.</param>
	/// <returns>The <typeparamref name="T"/> response configured with the provided status code where <see cref="ApiCallDetails.HasSuccessfulStatusCode"/> will be set using <paramref name="statusCodeRepresentsSuccess"/>.</returns>
	public static T CreateResponse<T>(T response, int httpStatusCode, bool statusCodeRepresentsSuccess) where T : TransportResponse
	{
		var apiCallDetails = new ApiCallDetails
		{
			HttpStatusCode = httpStatusCode,
			HasSuccessfulStatusCode = statusCodeRepresentsSuccess,
			HasExpectedContentType = true,
			TransportConfiguration = new TransportConfigurationDescriptor()
		};

		return CreateResponse<T>(response, apiCallDetails);
	}

	internal static T CreateResponse<T>(T response, ApiCallDetails apiCallDetails) where T : TransportResponse
	{
		response.ApiCallDetails = apiCallDetails;
		return response;
	}
}
