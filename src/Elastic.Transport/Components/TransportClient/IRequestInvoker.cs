// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Elastic.Transport;

/// <summary>
/// This interface abstracts the actual IO <see cref="ITransport{TConfiguration}"/> performs.
/// <para><see cref="ITransport{TConfiguration}"/> holds a single instance of this class</para>
/// <para>The instance to be used is provided to the constructor of <see cref="ITransportConfiguration"/> implementations</para>
/// <para>Where its exposed under <see cref="ITransportConfiguration.RequestInvoker"/></para>
/// </summary>
public interface IRequestInvoker : IDisposable
{
	/// <summary>
	/// Exposes the <see cref="Transport.ResponseFactory"/> used by the <see cref="IRequestInvoker"/>.
	/// </summary>
	public ResponseFactory ResponseFactory { get; }

	/// <summary>
	/// Perform a request to the endpoint described by <paramref name="boundConfiguration"/> using its associated configuration.
	/// </summary>
	/// <param name="endpoint">An object describing where to perform the IO call</param>
	/// <param name="boundConfiguration">An object describing how to perform the IO call</param>
	/// <param name="postData">Optional data to post</param>
	/// <param name="cancellationToken"></param>
	/// <typeparam name="TResponse">
	/// An implementation of <see cref="TransportResponse"/> ensuring enough information is available
	/// for <see cref="RequestPipeline"/> and <see cref="ITransport{TConfiguration}"/> to determine what to
	/// do with the response
	/// </typeparam>
	/// <returns>
	/// An implementation of <see cref="TransportResponse"/> ensuring enough information is available
	/// for <see cref="RequestPipeline"/> and <see cref="ITransport{TConfiguration}"/> to determine what to
	/// do with the response
	/// </returns>
	public Task<TResponse> RequestAsync<TResponse>(Endpoint endpoint, BoundConfiguration boundConfiguration, PostData? postData, CancellationToken cancellationToken)
		where TResponse : TransportResponse, new();

	/// <summary>
	/// Perform a request to the endpoint described by <paramref name="boundConfiguration"/> using its associated configuration.
	/// </summary>
	/// <param name="endpoint">An object describing where to perform the IO call</param>
	/// <param name="boundConfiguration">An object describing how to perform the IO call</param>
	/// <param name="postData">Optional data to post</param>
	/// <typeparam name="TResponse">
	/// An implementation of <see cref="TransportResponse"/> ensuring enough information is available
	/// for <see cref="RequestPipeline"/> and <see cref="ITransport{TConfiguration}"/> to determine what to
	/// do with the response
	/// </typeparam>
	/// <returns>
	/// An implementation of <see cref="TransportResponse"/> ensuring enough information is available
	/// for <see cref="RequestPipeline"/> and <see cref="ITransport{TConfiguration}"/> to determine what to
	/// do with the response
	/// </returns>
	public TResponse Request<TResponse>(Endpoint endpoint, BoundConfiguration boundConfiguration, PostData? postData)
		where TResponse : TransportResponse, new();
}
