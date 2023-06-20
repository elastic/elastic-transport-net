// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Elastic.Transport;

/// <summary>
/// This interface abstracts the actual IO <see cref="HttpTransport{TConnectionSettings}"/> performs.
/// <para><see cref="HttpTransport{TConnectionSettings}"/> holds a single instance of this class</para>
/// <para>The instance to be used is provided to the constructor of <see cref="ITransportConfiguration"/> implementations</para>
/// <para>Where its exposed under <see cref="ITransportConfiguration.Connection"/></para>
/// </summary>
public abstract class TransportClient : IDisposable
{
	private bool _disposed = false;

	/// <inheritdoc cref="TransportClient"/>
	protected TransportClient() { }

	/// <summary>
	/// Perform a request to the endpoint described by <paramref name="requestData"/> using its associated configuration.
	/// </summary>
	/// <param name="requestData">An object describing where and how to perform the IO call</param>
	/// <param name="cancellationToken"></param>
	/// <typeparam name="TResponse">
	/// An implementation of <see cref="TransportResponse"/> ensuring enough information is available
	/// for <see cref="RequestPipeline"/> and <see cref="HttpTransport{TConnectionSettings}"/> to determine what to
	/// do with the response
	/// </typeparam>
	/// <returns>
	/// An implementation of <see cref="TransportResponse"/> ensuring enough information is available
	/// for <see cref="RequestPipeline"/> and <see cref="HttpTransport{TConnectionSettings}"/> to determine what to
	/// do with the response
	/// </returns>
	public abstract Task<TResponse> RequestAsync<TResponse>(RequestData requestData, CancellationToken cancellationToken)
		where TResponse : TransportResponse, new();

	/// <summary>
	/// Perform a request to the endpoint described by <paramref name="requestData"/> using its associated configuration.
	/// </summary>
	/// <param name="requestData">An object describing where and how to perform the IO call</param>
	/// <typeparam name="TResponse">
	/// An implementation of <see cref="TransportResponse"/> ensuring enough information is available
	/// for <see cref="RequestPipeline"/> and <see cref="HttpTransport{TConnectionSettings}"/> to determine what to
	/// do with the response
	/// </typeparam>
	/// <returns>
	/// An implementation of <see cref="TransportResponse"/> ensuring enough information is available
	/// for <see cref="RequestPipeline"/> and <see cref="HttpTransport{TConnectionSettings}"/> to determine what to
	/// do with the response
	/// </returns>
	public abstract TResponse Request<TResponse>(RequestData requestData)
		where TResponse : TransportResponse, new();

	/// <summary>
	///
	/// </summary>
	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

	/// <summary>
	///
	/// </summary>
	/// <param name="disposing"></param>
	protected virtual void Dispose(bool disposing)
	{
		if (_disposed)
			return;

		if (disposing)
		{
			DisposeManagedResources();
		}

		_disposed = true;
	}

	/// <summary>
	///
	/// </summary>
	protected virtual void DisposeManagedResources() { }
}
