// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Threading;
using System.Threading.Tasks;
using Elastic.Transport.Diagnostics;

namespace Elastic.Transport;

/// <summary>
/// Represents a transport you can call requests, it is recommended to reference <see cref="ITransport{TConfiguration}" />
/// </summary>
public interface ITransport
{
	/// <summary>
	/// Orchestrate a request synchronously into a <see cref="NodePool"/> using the workflow defined in the <see cref="RequestPipeline"/>.
	/// </summary>
	/// <remarks>NOTE: It is highly recommended to prefer the asynchronous version of this method instead of this synchronous API.</remarks>
	/// <typeparam name="TResponse">The type to deserialize the response body into.</typeparam>
	/// <param name="method">The <see cref="HttpMethod"/> for the HTTP request.</param>
	/// <param name="path">The path of the request.</param>
	/// <returns>The deserialized <typeparamref name="TResponse"/>.</returns>
	public TResponse Request<TResponse>(
		HttpMethod method,
		string path)
		where TResponse : TransportResponse, new()
			=> Request<TResponse>(method, path, null, null, default);

#pragma warning disable 1573
	/// <inheritdoc cref="Request{TResponse}(HttpMethod, string)"/>
	/// <param name="postData">The data to be included as the body of the HTTP request.</param>
	public TResponse Request<TResponse>(
		HttpMethod method,
		string path,
		PostData? postData)
		where TResponse : TransportResponse, new()
			=> Request<TResponse>(method, path, postData, null, default);

	/// <inheritdoc cref="Request{TResponse}(HttpMethod, string, PostData?)"/>
	/// <param name="requestParameters">The parameters for the request.</param>
	public TResponse Request<TResponse>(
		HttpMethod method,
		string path,
		PostData? postData,
		RequestParameters? requestParameters)
		where TResponse : TransportResponse, new()
			=> Request<TResponse>(method, path, postData, requestParameters, default);

	/// <inheritdoc cref="Request{TResponse}(HttpMethod, string, PostData?, RequestParameters?)"/>
	/// <param name="openTelemetryData">Data to be used to control the OpenTelemetry instrumentation.</param>
	public TResponse Request<TResponse>(
		HttpMethod method,
		string path,
		PostData? postData,
		RequestParameters? requestParameters,
		in OpenTelemetryData openTelemetryData)
		where TResponse : TransportResponse, new();
#pragma warning restore 1573

	/// <summary>
	/// Orchestrate a request asynchronously into a <see cref="NodePool"/> using the workflow defined in the <see cref="RequestPipeline"/>.
	/// </summary>
	/// <typeparam name="TResponse">The type to deserialize the response body into.</typeparam>
	/// <param name="method">The <see cref="HttpMethod"/> for the HTTP request.</param>
	/// <param name="path">The path of the request.</param>
	/// <param name="cancellationToken">The cancellation token to use.</param>
	/// <returns>The deserialized <typeparamref name="TResponse"/>.</returns>
	public Task<TResponse> RequestAsync<TResponse>(
		HttpMethod method,
		string path,
		CancellationToken cancellationToken = default)
		where TResponse : TransportResponse, new()
			=> RequestAsync<TResponse>(method, path, null, null, default, cancellationToken);

#pragma warning disable 1573
	/// <inheritdoc cref="RequestAsync{TResponse}(HttpMethod, string, CancellationToken)"/>
	/// <param name="postData">The data to be included as the body of the HTTP request.</param>
	public Task<TResponse> RequestAsync<TResponse>(
		HttpMethod method,
		string path,
		PostData? postData,
		CancellationToken cancellationToken = default)
		where TResponse : TransportResponse, new()
			=> RequestAsync<TResponse>(method, path, postData, null, default, cancellationToken);

	/// <inheritdoc cref="RequestAsync{TResponse}(HttpMethod, string, PostData?, CancellationToken)"/>
	/// <param name="requestParameters">The parameters for the request.</param>
	public Task<TResponse> RequestAsync<TResponse>(
		HttpMethod method,
		string path,
		PostData? postData,
		RequestParameters? requestParameters,
		CancellationToken cancellationToken = default)
		where TResponse : TransportResponse, new()
			=> RequestAsync<TResponse>(method, path, postData, requestParameters, default, cancellationToken);

	/// <inheritdoc cref="RequestAsync{TResponse}(HttpMethod, string, PostData?, RequestParameters?, CancellationToken)"/>
	/// <param name="openTelemetryData">Data to be used to control the OpenTelemetry instrumentation.</param>
	public Task<TResponse> RequestAsync<TResponse>(
		HttpMethod method,
		string path,
		PostData? postData,
		RequestParameters? requestParameters,
		in OpenTelemetryData openTelemetryData,
		CancellationToken cancellationToken = default)
		where TResponse : TransportResponse, new();
#pragma warning restore 1573
}

/// <summary>
/// Transport coordinates the client requests over the node pool nodes and is in charge of falling over on
/// different nodes
/// </summary>
public interface ITransport<out TConfiguration> : ITransport
	where TConfiguration : class, ITransportConfiguration
{
	/// <summary>
	/// The <see cref="ITransportConfiguration" /> in use by this transport instance
	/// </summary>
	public TConfiguration Configuration { get; }
}
