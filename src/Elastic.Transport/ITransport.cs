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
	/// <param name="postData">The data to be included as the body of the HTTP request.</param>
	/// <param name="requestParameters">The parameters for the request.</param>
	/// <param name="openTelemetryData">Data to be used to control the OpenTelemetry instrumentation.</param>
	/// <returns>The deserialized <typeparamref name="TResponse"/>.</returns>
	public TResponse Request<TResponse>(
		HttpMethod method,
		string path,
		PostData? postData,
		RequestParameters? requestParameters,
		in OpenTelemetryData openTelemetryData)
		where TResponse : TransportResponse, new();


	/// <summary>
	/// Orchestrate a request asynchronously into a <see cref="NodePool"/> using the workflow defined in the <see cref="RequestPipeline"/>.
	/// </summary>
	/// <typeparam name="TResponse">The type to deserialize the response body into.</typeparam>
	/// <param name="method">The <see cref="HttpMethod"/> for the HTTP request.</param>
	/// <param name="path">The path of the request.</param>
	/// <param name="postData">The data to be included as the body of the HTTP request.</param>
	/// <param name="requestParameters">The parameters for the request.</param>
	/// <param name="cancellationToken">The cancellation token to use.</param>
	/// <param name="openTelemetryData">Data to be used to control the OpenTelemetry instrumentation.</param>
	/// <returns>The deserialized <typeparamref name="TResponse"/>.</returns>
	public Task<TResponse> RequestAsync<TResponse>(
		HttpMethod method,
		string path,
		PostData? postData,
		RequestParameters? requestParameters,
		in OpenTelemetryData openTelemetryData,
		CancellationToken cancellationToken = default)
		where TResponse : TransportResponse, new();
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

/// <summary>
/// Add request extension overloads so callees do not have to always pass all the parameters.
/// </summary>
public static class TransportExtensions
{

	/// <inheritdoc cref="ITransport.Request{TResponse}"/>>
	public static TResponse Request<TResponse>(
		this ITransport transport,
		HttpMethod method,
		string path)
		where TResponse : TransportResponse, new()
			=> transport.Request<TResponse>(method, path, null, null, default);

	/// <inheritdoc cref="ITransport.Request{TResponse}"/>>
	public static TResponse Request<TResponse>(
		this ITransport transport,
		HttpMethod method,
		string path,
		PostData? postData)
		where TResponse : TransportResponse, new()
			=> transport.Request<TResponse>(method, path, postData, null, default);

	/// <inheritdoc cref="ITransport.Request{TResponse}"/>>
	public static TResponse Request<TResponse>(
		this ITransport transport,
		HttpMethod method,
		string path,
		PostData? postData,
		RequestParameters? requestParameters)
		where TResponse : TransportResponse, new()
			=> transport.Request<TResponse>(method, path, postData, requestParameters, default);


	/// <inheritdoc cref="ITransport.RequestAsync{TResponse}"/>>
	public static Task<TResponse> RequestAsync<TResponse>(
		this ITransport transport,
		HttpMethod method,
		string path,
		CancellationToken cancellationToken = default)
		where TResponse : TransportResponse, new()
			=> transport.RequestAsync<TResponse>(method, path, null, null, default, cancellationToken);

	/// <inheritdoc cref="ITransport.RequestAsync{TResponse}"/>>
	public static Task<TResponse> RequestAsync<TResponse>(
		this ITransport transport,
		HttpMethod method,
		string path,
		PostData? postData,
		CancellationToken cancellationToken = default)
		where TResponse : TransportResponse, new()
			=> transport.RequestAsync<TResponse>(method, path, postData, null, default, cancellationToken);

	/// <inheritdoc cref="ITransport.RequestAsync{TResponse}"/>>
	public static Task<TResponse> RequestAsync<TResponse>(
		this ITransport transport,
		HttpMethod method,
		string path,
		PostData? postData,
		RequestParameters? requestParameters,
		CancellationToken cancellationToken = default)
		where TResponse : TransportResponse, new()
			=> transport.RequestAsync<TResponse>(method, path, postData, requestParameters, default, cancellationToken);
}
