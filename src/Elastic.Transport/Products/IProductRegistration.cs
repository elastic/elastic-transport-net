// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Elastic.Transport.Products
{
	/// <summary>
	/// When <see cref="ITransport.Request{TResponse}"/> interfaces with a product some parts are
	/// bespoke for each product. This interface defines the contract products will have to implement in order to fill
	/// in these bespoke parts.
	/// <para>The expectation is that unless you instantiate <see cref="Transport{TConnectionSettings}"/>
	/// directly clients that utilize transport will fill in this dependency
	/// </para>
	/// <para>
	/// If you do want to use a bare-bones <see cref="Transport{TConnectionSettings}"/> you can use
	/// <see cref="ProductRegistration.Default"/>
	/// </para>
	/// </summary>
	public interface IProductRegistration
	{
		/// <summary>
		/// The name of the current product utilizing <see cref="ITransport{TConnectionSettings}"/>
		/// <para>This name makes its way into the transport diagnostics sources and the default user agent string</para>
		/// </summary>
		string Name { get; }

		/// <summary>
		/// Whether the product <see cref="ITransport{TConnectionSettings}"/> will call out to supports ping endpoints
		/// </summary>
		bool SupportsPing { get; }

		/// <summary>
		/// Whether the product <see cref="ITransport{TConnectionSettings}"/> will call out to supports sniff endpoints that return
		/// information about available nodes
		/// </summary>
		bool SupportsSniff { get; }

		/// <summary>
		/// The set of headers to parse from all requests by default. These can be added to any consumer specific requirements.
		/// </summary>
		HeadersList ResponseHeadersToParse { get; }

		/// <summary>
		/// Create an instance of <see cref="RequestData"/> that describes where and how to ping see <paramref name="node" />
		/// <para>All the parameters of this method correspond with <see cref="RequestData"/>'s constructor</para>
		/// </summary>
		RequestData CreatePingRequestData(Node node, RequestConfiguration requestConfiguration, ITransportConfiguration global, IMemoryStreamFactory memoryStreamFactory);

		/// <summary>
		/// Provide an implementation that performs the ping directly using <see cref="ITransportClient.RequestAsync{TResponse}"/> and the <see cref="RequestData"/>
		/// return by <see cref="CreatePingRequestData"/>
		/// </summary>
		Task<ApiCallDetails> PingAsync(ITransportClient connection, RequestData pingData, CancellationToken cancellationToken);

		/// <summary>
		/// Provide an implementation that performs the ping directly using <see cref="ITransportClient.Request{TResponse}"/> and the <see cref="RequestData"/>
		/// return by <see cref="CreatePingRequestData"/>
		/// </summary>
		ApiCallDetails Ping(ITransportClient connection, RequestData pingData);

		/// <summary>
		/// Create an instance of <see cref="RequestData"/> that describes where and how to sniff the cluster using <paramref name="node" />
		/// <para>All the parameters of this method correspond with <see cref="RequestData"/>'s constructor</para>
		/// </summary>
		RequestData CreateSniffRequestData(Node node, IRequestConfiguration requestConfiguration, ITransportConfiguration settings,
			IMemoryStreamFactory memoryStreamFactory
		);

		/// <summary>
		/// Provide an implementation that performs the sniff directly using <see cref="ITransportClient.Request{TResponse}"/> and the <see cref="RequestData"/>
		/// return by <see cref="CreateSniffRequestData"/>
		/// </summary>
		Task<Tuple<ApiCallDetails, IReadOnlyCollection<Node>>> SniffAsync(ITransportClient connection, bool forceSsl, RequestData requestData, CancellationToken cancellationToken);

		/// <summary>
		/// Provide an implementation that performs the sniff directly using <see cref="ITransportClient.Request{TResponse}"/> and the <see cref="RequestData"/>
		/// return by <see cref="CreateSniffRequestData"/>
		/// </summary>
		Tuple<ApiCallDetails, IReadOnlyCollection<Node>> Sniff(ITransportClient connection, bool forceSsl, RequestData requestData);

		/// <summary> Allows certain nodes to be queried first to obtain sniffing information </summary>
		int SniffOrder(Node node);

		/// <summary> Predicate indicating a node is allowed to be used for API calls</summary>
		/// <param name="node">The node to inspect</param>
		/// <returns>bool, true if node should allows API calls</returns>
		bool NodePredicate(Node node);

		/// <summary>
		/// Used by <see cref="ResponseBuilder"/> to determine if it needs to return true or false for
		/// <see cref="ApiCallDetails.Success"/>
		/// </summary>
		bool HttpStatusCodeClassifier(HttpMethod method, int statusCode);

		/// <summary> Try to obtain a server error from the response, this is used for debugging and exception messages </summary>
		bool TryGetServerErrorReason<TResponse>(TResponse response, out string reason) where TResponse : TransportResponse;

		/// <summary>
		/// TODO
		/// </summary>
		MetaHeaderProvider MetaHeaderProvider { get; }

		/// <summary>
		/// TODO
		/// </summary>
		ResponseBuilder ResponseBuilder { get; }
	}
}
