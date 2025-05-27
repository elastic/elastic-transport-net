// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Diagnostics;
using System;
using System.Threading;
using System.Threading.Tasks;

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
	/// <param name="path">The path of the request.</param>
	/// <param name="postData">The data to be included as the body of the HTTP request.</param>
	/// <param name="configureActivity">An optional <see cref="Action"/> used to configure the <see cref="Activity"/>.</param>
	/// <param name="localConfiguration">Per request configuration</param>
	/// Allows callers to override completely how `TResponse` should be deserialized to a `TResponse` that implements <see cref="TransportResponse"/> instance.
	/// <para>Expert setting only</para>
	/// <returns>The deserialized <typeparamref name="TResponse"/>.</returns>
	public TResponse Request<TResponse>(
		in EndpointPath path,
		PostData? postData,
		Action<Activity>? configureActivity,
		IRequestConfiguration? localConfiguration
	)
		where TResponse : TransportResponse, new();

	/// <summary>
	/// Orchestrate a request asynchronously into a <see cref="NodePool"/> using the workflow defined in the <see cref="RequestPipeline"/>.
	/// </summary>
	/// <typeparam name="TResponse">The type to deserialize the response body into.</typeparam>
	/// <param name="path">The path of the request.</param>
	/// <param name="postData">The data to be included as the body of the HTTP request.</param>
	/// <param name="cancellationToken">The cancellation token to use.</param>
	/// <param name="configureActivity">An optional <see cref="Action"/> used to configure the <see cref="Activity"/>.</param>
	/// <param name="localConfiguration">Per request configuration</param>
	/// Allows callers to override completely how `TResponse` should be deserialized to a `TResponse` that implements <see cref="TransportResponse"/> instance.
	/// <para>Expert setting only</para>
	/// <returns>The deserialized <typeparamref name="TResponse"/>.</returns>
	public Task<TResponse> RequestAsync<TResponse>(
		in EndpointPath path,
		PostData? postData,
		Action<Activity>? configureActivity,
		IRequestConfiguration? localConfiguration,
		CancellationToken cancellationToken = default
	)
		where TResponse : TransportResponse, new();

	/// <summary> The <see cref="ITransportConfiguration" /> in use by this transport instance </summary>
	public ITransportConfiguration Configuration { get; }
}

/// <summary>
/// Transport coordinates the client requests over the node pool nodes and is in charge of falling over on
/// different nodes
/// </summary>
public interface ITransport<out TConfiguration> : ITransport
	where TConfiguration : class, ITransportConfiguration
{
	/// <summary> The <see cref="ITransportConfiguration" /> in use by this transport instance </summary>
	public new TConfiguration Configuration { get; }
}

/// <summary>
/// Add request extension overloads so callees do not have to always pass all the parameters.
/// </summary>
public static class TransportExtensions
{
	/// <inheritdoc cref="ITransport.Request{TResponse}"/>>
	public static TResponse Request<TResponse>(this ITransport transport, in EndpointPath path)
		where TResponse : TransportResponse, new()
			=> transport.Request<TResponse>(path, null, default, null);

	/// <inheritdoc cref="ITransport.Request{TResponse}"/>>
	public static TResponse Request<TResponse>(this ITransport transport, in EndpointPath path, PostData? postData)
		where TResponse : TransportResponse, new()
			=> transport.Request<TResponse>(path, postData, default, null);

	/// <inheritdoc cref="ITransport.Request{TResponse}"/>>
	public static TResponse Request<TResponse>(this ITransport transport, in EndpointPath path, PostData? postData, IRequestConfiguration configuration)
		where TResponse : TransportResponse, new()
			=> transport.Request<TResponse>(path, postData, default, configuration);

	/// <inheritdoc cref="ITransport.Request{TResponse}"/>>
	public static TResponse Request<TResponse>(this ITransport transport, HttpMethod method, string path)
		where TResponse : TransportResponse, new()
			=> transport.Request<TResponse>(new EndpointPath(method, path), null, default, null);

	/// <inheritdoc cref="ITransport.Request{TResponse}"/>>
	public static TResponse Request<TResponse>(this ITransport transport, HttpMethod method, string path, PostData? postData)
		where TResponse : TransportResponse, new()
			=> transport.Request<TResponse>(new EndpointPath(method, path), postData, default, null);

	/// <inheritdoc cref="ITransport.Request{TResponse}"/>>
	public static TResponse Request<TResponse>(
		this ITransport transport,
		HttpMethod method,
		string path,
		PostData? postData,
		IRequestConfiguration localConfiguration)
		where TResponse : TransportResponse, new()
			=> transport.Request<TResponse>(new EndpointPath(method, path), postData, default, localConfiguration);

	/// <inheritdoc cref="ITransport.RequestAsync{TResponse}"/>>
	public static Task<TResponse> RequestAsync<TResponse>(this ITransport transport, in EndpointPath path, CancellationToken cancellationToken = default)
		where TResponse : TransportResponse, new()
			=> transport.RequestAsync<TResponse>(path, null, default, null, cancellationToken);

	/// <inheritdoc cref="ITransport.RequestAsync{TResponse}"/>>
	public static Task<TResponse> RequestAsync<TResponse>(this ITransport transport, in EndpointPath path, PostData? postData, CancellationToken cancellationToken = default)
		where TResponse : TransportResponse, new()
			=> transport.RequestAsync<TResponse>(path, postData, default, null, cancellationToken);

	/// <inheritdoc cref="ITransport.RequestAsync{TResponse}"/>>
	public static Task<TResponse> RequestAsync<TResponse>(
		this ITransport transport,
		HttpMethod method,
		string path,
		CancellationToken cancellationToken = default)
		where TResponse : TransportResponse, new()
			=> transport.RequestAsync<TResponse>(new EndpointPath(method, path), null, default, null, cancellationToken);

	/// <inheritdoc cref="ITransport.RequestAsync{TResponse}"/>>
	public static Task<TResponse> RequestAsync<TResponse>(
		this ITransport transport,
		HttpMethod method,
		string path,
		PostData? postData,
		CancellationToken cancellationToken = default)
		where TResponse : TransportResponse, new()
			=> transport.RequestAsync<TResponse>(new EndpointPath(method, path), postData, default, null, cancellationToken);

	/// <inheritdoc cref="ITransport.RequestAsync{TResponse}"/>>
	public static Task<TResponse> RequestAsync<TResponse>(
		this ITransport transport,
		HttpMethod method,
		string path,
		PostData? postData,
		IRequestConfiguration localConfiguration,
		CancellationToken cancellationToken = default)
		where TResponse : TransportResponse, new()
			=> transport.RequestAsync<TResponse>(new EndpointPath(method, path), postData, default, localConfiguration, cancellationToken);
}
