// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Elastic.Transport
{
	/// <summary>
	/// This interface abstracts the actual IO <see cref="ITransport{TConnectionSettings}"/> performs.
	/// <para><see cref="ITransport{TConnectionSettings}"/> holds a single instance of this class</para>
	/// <para>The instance to be used is provided to the constructor of <see cref="ITransportConfiguration"/> implementations</para>
	/// <para>Where its exposed under <see cref="ITransportConfiguration.Connection"/></para>
	/// </summary>
	public interface IConnection : IDisposable
	{
		/// <summary>
		/// Perform a request to the endpoint described by <paramref name="requestData"/> using its associated configuration.
		/// </summary>
		/// <param name="requestData">An object describing where and how to perform the IO call</param>
		/// <param name="cancellationToken"></param>
		/// <typeparam name="TResponse">
		/// An implementation of <see cref="ITransportResponse"/> ensuring enough information is available
		/// for <see cref="IRequestPipeline"/> and <see cref="ITransport{TConnectionSettings}"/> to determine what to
		/// do with the response
		/// </typeparam>
		/// <returns>
		/// An implementation of <see cref="ITransportResponse"/> ensuring enough information is available
		/// for <see cref="IRequestPipeline"/> and <see cref="ITransport{TConnectionSettings}"/> to determine what to
		/// do with the response
		/// </returns>
		Task<TResponse> RequestAsync<TResponse>(RequestData requestData, CancellationToken cancellationToken)
			where TResponse : class, ITransportResponse, new();

		/// <inheritdoc cref="Request{TResponse}"/>
		Task<TResponse> RequestAsync<TResponse, TError>(RequestData requestData, CancellationToken cancellationToken)
			where TResponse : class, ITransportResponse<TError>, new()
			where TError : class, IErrorResponse, new();

		/// <summary>
		/// Perform a request to the endpoint described by <paramref name="requestData"/> using its associated configuration.
		/// </summary>
		/// <param name="requestData">An object describing where and how to perform the IO call</param>
		/// <typeparam name="TResponse">
		/// An implementation of <see cref="ITransportResponse"/> ensuring enough information is available
		/// for <see cref="IRequestPipeline"/> and <see cref="ITransport{TConnectionSettings}"/> to determine what to
		/// do with the response
		/// </typeparam>
		/// <returns>
		/// An implementation of <see cref="ITransportResponse"/> ensuring enough information is available
		/// for <see cref="IRequestPipeline"/> and <see cref="ITransport{TConnectionSettings}"/> to determine what to
		/// do with the response
		/// </returns>
		TResponse Request<TResponse>(RequestData requestData)
			where TResponse : class, ITransportResponse, new();

		/// <inheritdoc cref="Request{TResponse}"/>
		TResponse Request<TResponse, TError>(RequestData requestData)
			where TResponse : class, ITransportResponse<TError>, new()
			where TError : class, IErrorResponse, new();
	}
}
