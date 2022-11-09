// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Threading;
using System.Threading.Tasks;

namespace Elastic.Transport
{
	/// <summary>
	/// Represents a transport you can call requests, it is recommended to implement <see cref="ITransport{TSettings}" />
	/// </summary>
	public interface ITransport
	{
		/// <summary>
		/// Perform a request into the products cluster using <see cref="IRequestPipeline" />'s workflow.
		/// </summary>
		TResponse Request<TResponse>(
			HttpMethod method,
			string path,
			PostData data = null,
			RequestParameters requestParameters = null)
			where TResponse : TransportResponse, new();

		/// <inheritdoc cref="Request{TResponse}" />
		Task<TResponse> RequestAsync<TResponse>(
			HttpMethod method,
			string path,
			PostData data = null,
			RequestParameters requestParameters = null,
			CancellationToken cancellationToken = default)
			where TResponse : TransportResponse, new();
	}

	/// <summary>
	/// Transport coordinates the client requests over the node pool nodes and is in charge of falling over on
	/// different nodes
	/// </summary>
	public interface ITransport<out TConfiguration> : ITransport
		where TConfiguration : ITransportConfiguration
	{
		/// <summary>
		/// The <see cref="ITransportConfiguration" /> in use by this transport instance
		/// </summary>
		TConfiguration Settings { get; }
	}
}
