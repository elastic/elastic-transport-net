// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Threading;
using System.Threading.Tasks;

namespace Elastic.Transport;

/// <summary>
///     Extends <see cref="ITransport" /> with some convenience methods to make it easier to perform specific requests
/// </summary>
public static class TransportHttpMethodExtensions
{
	/// <summary>Perform a GET request</summary>
	public static TResponse Get<TResponse>(this ITransport requestHandler, string path,
		RequestParameters? parameters = null)
		where TResponse : TransportResponse, new() =>
		requestHandler.Request<TResponse>(HttpMethod.GET, path, null, parameters);

	/// <summary>Perform a GET request</summary>
	public static Task<TResponse> GetAsync<TResponse>(this ITransport requestHandler, string path,
		RequestParameters? parameters = null, CancellationToken cancellationToken = default)
		where TResponse : TransportResponse, new() =>
		requestHandler.RequestAsync<TResponse>(HttpMethod.GET, path, null, parameters, cancellationToken);

	/// <summary>Perform a HEAD request</summary>
	public static TResponse Head<TResponse>(this ITransport requestHandler, string path,
		RequestParameters? parameters = null)
		where TResponse : TransportResponse, new() =>
		requestHandler.Request<TResponse>(HttpMethod.HEAD, path, null, parameters);

	/// <summary>Perform a HEAD request</summary>
	public static Task<TResponse> HeadAsync<TResponse>(this ITransport requestHandler, string path,
		RequestParameters? parameters = null, CancellationToken cancellationToken = default)
		where TResponse : TransportResponse, new() =>
		requestHandler.RequestAsync<TResponse>(HttpMethod.HEAD, path, null, parameters, cancellationToken);

	/// <summary>Perform a HEAD request</summary>
	public static VoidResponse Head(this ITransport requestHandler, string path, RequestParameters? parameters = null) =>
		requestHandler.Head<VoidResponse>(path, parameters);

	/// <summary>Perform a HEAD request</summary>
	public static Task<VoidResponse> HeadAsync(this ITransport requestHandler, string path,
		RequestParameters? parameters = null, CancellationToken cancellationToken = default) =>
		requestHandler.HeadAsync<VoidResponse>(path, parameters, cancellationToken);

	/// <summary>Perform a POST request</summary>
	public static TResponse Post<TResponse>(this ITransport requestHandler, string path, PostData data,
		RequestParameters? parameters = null)
		where TResponse : TransportResponse, new() =>
		requestHandler.Request<TResponse>(HttpMethod.POST, path, data, parameters);

	/// <summary>Perform a POST request</summary>
	public static Task<TResponse> PostAsync<TResponse>(this ITransport requestHandler, string path, PostData data,
		RequestParameters? parameters = null, CancellationToken cancellationToken = default)
		where TResponse : TransportResponse, new() =>
		requestHandler.RequestAsync<TResponse>(HttpMethod.POST, path, data, parameters, cancellationToken);

	/// <summary>Perform a PUT request</summary>
	public static TResponse Put<TResponse>(this ITransport requestHandler, string path, PostData data,
		RequestParameters? parameters = null)
		where TResponse : TransportResponse, new() =>
		requestHandler.Request<TResponse>(HttpMethod.PUT, path, data, parameters);

	/// <summary>Perform a PUT request</summary>
	public static Task<TResponse> PutAsync<TResponse>(this ITransport requestHandler, string path, PostData data,
		RequestParameters? parameters = null, CancellationToken cancellationToken = default)
		where TResponse : TransportResponse, new() =>
		requestHandler.RequestAsync<TResponse>(HttpMethod.PUT, path, data, parameters, cancellationToken);

	/// <summary>Perform a DELETE request</summary>
	public static TResponse Delete<TResponse>(this ITransport requestHandler, string path, PostData? data = null,
		RequestParameters? parameters = null)
		where TResponse : TransportResponse, new() =>
		requestHandler.Request<TResponse>(HttpMethod.DELETE, path, data, parameters);

	/// <summary>Perform a DELETE request</summary>
	public static Task<TResponse> DeleteAsync<TResponse>(this ITransport requestHandler, string path,
		PostData? data = null, RequestParameters? parameters = null, CancellationToken cancellationToken = default)
		where TResponse : TransportResponse, new() =>
		requestHandler.RequestAsync<TResponse>(HttpMethod.DELETE, path, data, parameters, cancellationToken);
}
