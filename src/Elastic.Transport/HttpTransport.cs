// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Threading;
using System.Threading.Tasks;
using Elastic.Transport.Diagnostics;

namespace Elastic.Transport;

/// <summary>
/// Represents a transport you can call requests, it is recommended to implement <see cref="HttpTransport{TSettings}" />
/// </summary>
public abstract class HttpTransport
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
	public abstract TResponse Request<TResponse>(
		HttpMethod method,
		string path,
		PostData? postData = null,
		RequestParameters? requestParameters = null,
		in OpenTelemetryData? openTelemetryData = null)
		where TResponse : TransportResponse, new();
#pragma warning restore 1573

	/// <summary>
	/// Orchestrate a request asynchronously into a <see cref="NodePool"/> using the workflow defined in the <see cref="RequestPipeline"/>.
	/// </summary>
	/// <typeparam name="TResponse">The type to deserialize the response body into.</typeparam>
	/// <param name="method">The <see cref="HttpMethod"/> for the HTTP request.</param>
	/// <param name="path">The path of the request.</param>
	/// <param name="postData">The data to be included as the body of the HTTP request.</param>
	/// <param name="requestParameters">The parameters for the request.</param>
	/// <param name="openTelemetryData">Data to be used to control the OpenTelemetry instrumentation.</param>
	/// <param name="cancellationToken">The cancellation token to use.</param>
	/// <returns>The deserialized <typeparamref name="TResponse"/>.</returns>
	public abstract Task<TResponse> RequestAsync<TResponse>(
		HttpMethod method,
		string path,
		PostData? postData = null,
		RequestParameters? requestParameters = null,
		in OpenTelemetryData? openTelemetryData = null,
		CancellationToken cancellationToken = default)
		where TResponse : TransportResponse, new();
#pragma warning restore 1573
}

/// <summary>
/// Transport coordinates the client requests over the node pool nodes and is in charge of falling over on
/// different nodes
/// </summary>
public abstract class HttpTransport<TConfiguration> : HttpTransport
	where TConfiguration : class, ITransportConfiguration
{
	/// <summary>
	/// The <see cref="ITransportConfiguration" /> in use by this transport instance
	/// </summary>
	public abstract TConfiguration Settings { get; }
}
