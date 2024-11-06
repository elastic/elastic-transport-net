// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Elastic.Transport;

/// <summary>
/// A builder that handles one or more response types derived from <see cref="TransportResponse"/>.
/// </summary>
public interface IResponseBuilder
{
	/// <summary>
	/// Determines whether the builder can build a <typeparamref name="TResponse"/>.
	/// </summary>
	/// <typeparam name="TResponse">The response type to be built.</typeparam>
	/// <returns>A <c>bool</c> which indicates whether the builder can build the <typeparamref name="TResponse"/>.</returns>
	bool CanBuild<TResponse>() where TResponse : TransportResponse, new();

	/// <summary>
	/// Build a <typeparamref name="TResponse"/> from the supplied <see cref="Stream"/>.
	/// </summary>
	/// <typeparam name="TResponse">The specific type of the <see cref="TransportResponse"/> to be built.</typeparam>
	/// <param name="apiCallDetails">The initialized <see cref="ApiCallDetails"/> for the response.</param>
	/// <param name="requestData">The <see cref="RequestData"/> for the HTTP request.</param>
	/// <param name="responseStream">The readable <see cref="Stream"/> containing the response body.</param>
	/// <param name="contentType">The value of the Content-Type header for the response.</param>
	/// <param name="contentLength">The length of the content, if available in the response headers.</param>
	/// <returns>A potentiall null response of type <typeparamref name="TResponse"/>.</returns>
	TResponse? Build<TResponse>(ApiCallDetails apiCallDetails, RequestData requestData,
		Stream responseStream, string contentType, long contentLength) where TResponse : TransportResponse, new();

	/// <summary>
	/// Build a <typeparamref name="TResponse"/> from the supplied <see cref="Stream"/>.
	/// </summary>
	/// <typeparam name="TResponse">The specific type of the <see cref="TransportResponse"/> to be built.</typeparam>
	/// <param name="apiCallDetails">The initialized <see cref="ApiCallDetails"/> for the response.</param>
	/// <param name="requestData">The <see cref="RequestData"/> for the HTTP request.</param>
	/// <param name="responseStream">The readable <see cref="Stream"/> containing the response body.</param>
	/// <param name="contentType">The value of the Content-Type header for the response.</param>
	/// <param name="contentLength">The length of the content, if available in the response headers.</param>
	/// <param name="cancellationToken">An optional <see cref="CancellationToken"/> that can trigger cancellation.</param>
	/// <returns>A potentiall null response of type <typeparamref name="TResponse"/>.</returns>
	Task<TResponse?> BuildAsync<TResponse>(ApiCallDetails apiCallDetails, RequestData requestData,
		Stream responseStream, string contentType, long contentLength, CancellationToken cancellationToken = default) where TResponse : TransportResponse, new();
}
