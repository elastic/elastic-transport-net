// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Threading;
using System.Threading.Tasks;
using static Elastic.Transport.HttpMethod;

namespace Elastic.Transport;

/// <summary>
///     Extends <see cref="ITransport" /> with some convenience methods to make it easier to perform specific requests
/// </summary>
public static class TransportHttpMethodExtensions
{
	private static EndpointPath ToEndpointPath(HttpMethod method, string path, RequestParameters parameters, ITransportConfiguration configuration) =>
		new(method, parameters.CreatePathWithQueryStrings(path, configuration));

	/// <summary>Perform a GET request</summary>
	public static TResponse Get<TResponse>(this ITransport<ITransportConfiguration> transport, string path, RequestParameters parameters)
		where TResponse : TransportResponse, new() =>
		transport.Request<TResponse>(ToEndpointPath(GET, path, parameters, transport.Configuration), postData: null, null, null);

	/// <summary>Perform a GET request</summary>
	public static Task<TResponse> GetAsync<TResponse>(this ITransport<ITransportConfiguration> transport, string path,
		RequestParameters parameters, CancellationToken cancellationToken = default)
		where TResponse : TransportResponse, new() =>
		transport.RequestAsync<TResponse>(ToEndpointPath(GET, path, parameters, transport.Configuration), postData: null, null, null, cancellationToken);

	/// <summary>Perform a GET request</summary>
	public static TResponse Get<TResponse>(this ITransport transport, string pathAndQuery)
		where TResponse : TransportResponse, new() =>
		transport.Request<TResponse>(new EndpointPath(GET, pathAndQuery), postData: null, null, null);

	/// <summary>Perform a GET request</summary>
	public static Task<TResponse> GetAsync<TResponse>(this ITransport transport, string pathAndQuery, CancellationToken cancellationToken = default)
		where TResponse : TransportResponse, new() =>
		transport.RequestAsync<TResponse>(new EndpointPath(GET, pathAndQuery), postData: null, null, null, cancellationToken);



	/// <summary>Perform a HEAD request</summary>
	public static VoidResponse Head(this ITransport<ITransportConfiguration> transport, string path, RequestParameters parameters)
		 => transport.Request<VoidResponse>(ToEndpointPath(HEAD, path, parameters, transport.Configuration), postData: null, null, null);

	/// <summary>Perform a HEAD request</summary>
	public static Task<VoidResponse> HeadAsync(this ITransport<ITransportConfiguration> transport, string path, RequestParameters parameters, CancellationToken cancellationToken = default)
		=> transport.RequestAsync<VoidResponse>(ToEndpointPath(HEAD, path, parameters, transport.Configuration), postData: null, null, null, cancellationToken);

	/// <summary>Perform a HEAD request</summary>
	public static VoidResponse Head(this ITransport transport, string pathAndQuery)
		 => transport.Request<VoidResponse>(new EndpointPath(HEAD, pathAndQuery), postData: null, null, null);

	/// <summary>Perform a HEAD request</summary>
	public static Task<VoidResponse> HeadAsync(this ITransport transport, string pathAndQuery, CancellationToken cancellationToken = default)
		=> transport.RequestAsync<VoidResponse>(new EndpointPath(HEAD, pathAndQuery), postData: null, null, null, cancellationToken);

	/// <summary>Perform a HEAD request</summary>
	public static VoidResponse Head(this ITransport transport, string pathAndQuery, TimeSpan timeout)
		=> transport.Request<VoidResponse>(new EndpointPath(HEAD, pathAndQuery), postData: null, null, new RequestConfiguration { RequestTimeout = timeout });

	/// <summary>Perform a HEAD request</summary>
	public static Task<VoidResponse> HeadAsync(this ITransport transport, string pathAndQuery, TimeSpan timeout, CancellationToken cancellationToken = default)
		=> transport.RequestAsync<VoidResponse>(new EndpointPath(HEAD, pathAndQuery), postData: null, null, new RequestConfiguration { RequestTimeout = timeout }, cancellationToken);



	/// <summary>Perform a POST request</summary>
	public static TResponse Post<TResponse>(this ITransport<ITransportConfiguration> transport, string path, PostData data, RequestParameters parameters)
		where TResponse : TransportResponse, new() =>
		transport.Request<TResponse>(ToEndpointPath(POST, path, parameters, transport.Configuration), data, null, null);

	/// <summary>Perform a POST request</summary>
	public static Task<TResponse> PostAsync<TResponse>(this ITransport<ITransportConfiguration> transport, string path, PostData data,
		RequestParameters parameters, CancellationToken cancellationToken = default)
		where TResponse : TransportResponse, new() =>
		transport.RequestAsync<TResponse>(ToEndpointPath(POST, path, parameters, transport.Configuration), data, null, null, cancellationToken);

	/// <summary>Perform a POST request</summary>
	public static TResponse Post<TResponse>(this ITransport transport, string pathAndQuery, PostData data)
		where TResponse : TransportResponse, new() =>
		transport.Request<TResponse>(new EndpointPath(POST, pathAndQuery), data, null, null);

	/// <summary>Perform a POST request</summary>
	public static Task<TResponse> PostAsync<TResponse>(this ITransport transport, string pathAndQuery, PostData data, CancellationToken cancellationToken = default)
		where TResponse : TransportResponse, new() =>
		transport.RequestAsync<TResponse>(new EndpointPath(POST, pathAndQuery), data, null, null, cancellationToken);

	/// <summary>Perform a POST request</summary>
	public static TResponse Post<TResponse>(this ITransport transport, string pathAndQuery, PostData data, TimeSpan timeout)
		where TResponse : TransportResponse, new() =>
		transport.Request<TResponse>(new EndpointPath(POST, pathAndQuery), data, null, new RequestConfiguration { RequestTimeout = timeout });

	/// <summary>Perform a POST request</summary>
	public static Task<TResponse> PostAsync<TResponse>(this ITransport transport, string pathAndQuery, PostData data, TimeSpan timeout, CancellationToken cancellationToken = default)
		where TResponse : TransportResponse, new() =>
		transport.RequestAsync<TResponse>(new EndpointPath(POST, pathAndQuery), data, null, new RequestConfiguration { RequestTimeout = timeout }, cancellationToken);


	/// <summary>Perform a PUT request</summary>
	public static TResponse Put<TResponse>(this ITransport<ITransportConfiguration> transport, string path, PostData data, RequestParameters parameters)
		where TResponse : TransportResponse, new() =>
		transport.Request<TResponse>(ToEndpointPath(PUT, path, parameters, transport.Configuration), data, null, null);

	/// <summary>Perform a PUT request</summary>
	public static Task<TResponse> PutAsync<TResponse>(this ITransport<ITransportConfiguration> transport, string path, PostData data, RequestParameters parameters, CancellationToken cancellationToken = default)
		where TResponse : TransportResponse, new() =>
		transport.RequestAsync<TResponse>(ToEndpointPath(PUT, path, parameters, transport.Configuration), data, null, null, cancellationToken);

	/// <summary>Perform a PUT request</summary>
	public static TResponse Put<TResponse>(this ITransport transport, string pathAndQuery, PostData data)
		where TResponse : TransportResponse, new() =>
		transport.Request<TResponse>(new EndpointPath(PUT, pathAndQuery), data, null, null);

	/// <summary>Perform a PUT request</summary>
	public static Task<TResponse> PutAsync<TResponse>(this ITransport transport, string pathAndQuery, PostData data, CancellationToken cancellationToken = default)
		where TResponse : TransportResponse, new() =>
		transport.RequestAsync<TResponse>(new EndpointPath(PUT, pathAndQuery), data, null, null, cancellationToken);

	/// <summary>Perform a PUT request</summary>
	public static TResponse Put<TResponse>(this ITransport transport, string pathAndQuery, PostData data, TimeSpan timeout)
		where TResponse : TransportResponse, new() =>
		transport.Request<TResponse>(new EndpointPath(PUT, pathAndQuery), data, null, new RequestConfiguration { RequestTimeout = timeout });

	/// <summary>Perform a PUT request</summary>
	public static Task<TResponse> PutAsync<TResponse>(this ITransport transport, string pathAndQuery, PostData data, TimeSpan timeout, CancellationToken cancellationToken = default)
		where TResponse : TransportResponse, new() =>
		transport.RequestAsync<TResponse>(new EndpointPath(PUT, pathAndQuery), data, null, new RequestConfiguration { RequestTimeout = timeout }, cancellationToken);


	/// <summary>Perform a DELETE request</summary>
	public static TResponse Delete<TResponse>(this ITransport<ITransportConfiguration> transport, string path, RequestParameters parameters, PostData? data = null)
		where TResponse : TransportResponse, new() =>
		transport.Request<TResponse>(ToEndpointPath(DELETE, path, parameters, transport.Configuration), data, null, null);

	/// <summary>Perform a DELETE request</summary>
	public static Task<TResponse> DeleteAsync<TResponse>(this ITransport<ITransportConfiguration> transport, string path, RequestParameters parameters, PostData? data = null, CancellationToken cancellationToken = default)
		where TResponse : TransportResponse, new() =>
		transport.RequestAsync<TResponse>(ToEndpointPath(DELETE, path, parameters, transport.Configuration), data, null, null, cancellationToken);

	/// <summary>Perform a DELETE request</summary>
	public static TResponse Delete<TResponse>(this ITransport transport, string pathAndQuery, PostData? data = null, TimeSpan? timeout = null)
		where TResponse : TransportResponse, new() =>
		transport.Request<TResponse>(new EndpointPath(DELETE, pathAndQuery), data, null, timeout == null ? null : new RequestConfiguration { RequestTimeout = timeout });

	/// <summary>Perform a DELETE request</summary>
	public static Task<TResponse> DeleteAsync<TResponse>(this ITransport transport, string pathAndQuery, PostData? data = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
		where TResponse : TransportResponse, new() =>
		transport.RequestAsync<TResponse>(new EndpointPath(DELETE, pathAndQuery), data, null, timeout == null ? null : new RequestConfiguration { RequestTimeout = timeout }, cancellationToken);

}
