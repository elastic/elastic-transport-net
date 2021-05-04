// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Threading;
using System.Threading.Tasks;

namespace Elastic.Transport
{
	/// <summary>
	/// Extends <see cref="ITransport"/> with some convenience methods to make it easier to perform specific requests
	/// </summary>
	public static class TransportExtensions
	{

		/// <summary>Perform a GET request</summary>
		public static TResponse Get<TResponse>(this ITransport transport, string path, IRequestParameters parameters = null)
			where TResponse : class, ITransportResponse, new() =>
			transport.Request<TResponse>(HttpMethod.GET, path, null, parameters);

		/// <summary>Perform a GET request</summary>
		public static Task<TResponse> GetAsync<TResponse>(this ITransport transport, string path, CancellationToken ctx = default, IRequestParameters parameters = null)
			where TResponse : class, ITransportResponse, new() =>
			transport.RequestAsync<TResponse>(HttpMethod.GET, path, ctx, null, parameters);

		/// <summary>Perform a HEAD request</summary>
		public static TResponse Head<TResponse>(this ITransport transport, string path, IRequestParameters parameters = null)
			where TResponse : class, ITransportResponse, new() =>
			transport.Request<TResponse>(HttpMethod.HEAD, path, null, parameters);

		/// <summary>Perform a HEAD request</summary>
		public static Task<TResponse> HeadAsync<TResponse>(this ITransport transport, string path, CancellationToken ctx = default, IRequestParameters parameters = null)
			where TResponse : class, ITransportResponse, new() =>
			transport.RequestAsync<TResponse>(HttpMethod.HEAD, path, ctx, null, parameters);

		/// <summary>Perform a HEAD request</summary>
		public static VoidResponse Head(this ITransport transport, string path, IRequestParameters parameters = null) =>
			transport.Head<VoidResponse>(path, parameters);

		/// <summary>Perform a HEAD request</summary>
		public static Task<VoidResponse> HeadAsync(this ITransport transport, string path, CancellationToken ctx = default, IRequestParameters parameters = null) =>
			transport.HeadAsync<VoidResponse>(path, ctx, parameters);

		/// <summary>Perform a POST request</summary>
		public static TResponse Post<TResponse>(this ITransport transport, string path, PostData data, IRequestParameters parameters = null)
			where TResponse : class, ITransportResponse, new() =>
			transport.Request<TResponse>(HttpMethod.POST, path, data, parameters);

		/// <summary>Perform a POST request</summary>
		public static Task<TResponse> PostAsync<TResponse>(this ITransport transport, string path, PostData data, CancellationToken ctx = default, IRequestParameters parameters = null)
			where TResponse : class, ITransportResponse, new() =>
			transport.RequestAsync<TResponse>(HttpMethod.POST, path, ctx, data, parameters);

		/// <summary>Perform a PUT request</summary>
		public static TResponse Put<TResponse>(this ITransport transport, string path, PostData data, IRequestParameters parameters = null)
			where TResponse : class, ITransportResponse, new() =>
			transport.Request<TResponse>(HttpMethod.PUT, path, data, parameters);

		/// <summary>Perform a PUT request</summary>
		public static Task<TResponse> PutAsync<TResponse>(this ITransport transport, string path, PostData data, CancellationToken ctx = default, IRequestParameters parameters = null)
			where TResponse : class, ITransportResponse, new() =>
			transport.RequestAsync<TResponse>(HttpMethod.PUT, path, ctx, data, parameters);

		/// <summary>Perform a DELETE request</summary>
		public static TResponse Delete<TResponse>(this ITransport transport, string path, PostData data = null, IRequestParameters parameters = null)
			where TResponse : class, ITransportResponse, new() =>
			transport.Request<TResponse>(HttpMethod.DELETE, path, data, parameters);

		/// <summary>Perform a DELETE request</summary>
		public static Task<TResponse> DeleteAsync<TResponse>(this ITransport transport, string path, PostData data = null, CancellationToken ctx = default, IRequestParameters parameters = null)
			where TResponse : class, ITransportResponse, new() =>
			transport.RequestAsync<TResponse>(HttpMethod.DELETE, path, ctx, data, parameters);

	}
}
