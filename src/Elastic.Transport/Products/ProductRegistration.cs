// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Elastic.Transport.Products;

/// <summary>
/// When a request interfaces with a product, some parts are
/// bespoke for each product. This interface defines the contract products will have to implement in order to fill
/// in these bespoke parts.
/// <para>The expectation is that unless you instantiate <see cref="DistributedTransport{TConfiguration}"/>
/// directly clients that utilize transport will fill in this dependency
/// </para>
/// <para>
/// If you do want to use a bare-bones <see cref="DistributedTransport{TConfiguration}"/> you can use
/// <see cref="DefaultProductRegistration.Default"/>
/// </para>
/// </summary>
public abstract class ProductRegistration
{
	/// <summary>
	/// The default MIME type used for Accept and Content-Type headers for requests.
	/// </summary>
	public abstract string DefaultContentType { get; }

	/// <summary>
	/// The name of the current product utilizing <see cref="ITransport{TConfiguration}"/>
	/// <para>This name makes its way into the transport diagnostics sources and the default user agent string</para>
	/// </summary>
	public abstract string Name { get; }

	/// <summary>
	/// An optional service-identifier string that is used in metadata headers.
	/// </summary>
	public abstract string? ServiceIdentifier { get; }

	/// <summary>
	/// Whether the product <see cref="ITransport{TConfiguration}"/> will call out to supports ping endpoints
	/// </summary>
	public abstract bool SupportsPing { get; }

	/// <summary>
	/// Whether the product <see cref="ITransport{TConfiguration}"/> will call out to supports sniff endpoints that return
	/// information about available nodes
	/// </summary>
	public abstract bool SupportsSniff { get; }

	/// <summary>
	/// The set of headers to parse from all requests by default. These can be added to any consumer specific requirements.
	/// </summary>
	public abstract HeadersList ResponseHeadersToParse { get; }

	/// <summary>
	/// Create an instance of <see cref="BoundConfiguration"/> that describes where and how to ping see <paramref name="node" />
	/// <para>All the parameters of this method correspond with <see cref="BoundConfiguration"/>'s constructor</para>
	/// </summary>
	public abstract Endpoint CreatePingEndpoint(Node node, IRequestConfiguration requestConfiguration);

	/// <summary>
	/// Provide an implementation that performs the ping directly using <see cref="IRequestInvoker.RequestAsync{TResponse}"/> and the <see cref="BoundConfiguration"/>
	/// return by <see cref="CreatePingEndpoint"/>
	/// </summary>
	public abstract Task<TransportResponse> PingAsync(IRequestInvoker requestInvoker, Endpoint endpoint, BoundConfiguration boundConfiguration, CancellationToken cancellationToken);

	/// <summary>
	/// Provide an implementation that performs the ping directly using <see cref="IRequestInvoker.Request{TResponse}"/> and the <see cref="BoundConfiguration"/>
	/// return by <see cref="CreatePingEndpoint"/>
	/// </summary>
	public abstract TransportResponse Ping(IRequestInvoker requestInvoker, Endpoint endpoint, BoundConfiguration boundConfiguration);

	/// <summary>
	/// Create an instance of <see cref="BoundConfiguration"/> that describes where and how to sniff the cluster using <paramref name="node" />
	/// <para>All the parameters of this method correspond with <see cref="BoundConfiguration"/>'s constructor</para>
	/// </summary>
	public abstract Endpoint CreateSniffEndpoint(Node node, IRequestConfiguration requestConfiguration, ITransportConfiguration settings);

	/// <summary>
	/// Provide an implementation that performs the sniff directly using <see cref="IRequestInvoker.Request{TResponse}"/> and the <see cref="BoundConfiguration"/>
	/// return by <see cref="CreateSniffEndpoint"/>
	/// </summary>
	public abstract Task<Tuple<TransportResponse, IReadOnlyCollection<Node>>> SniffAsync(IRequestInvoker requestInvoker, bool forceSsl, Endpoint endpoint, BoundConfiguration boundConfiguration, CancellationToken cancellationToken);

	/// <summary>
	/// Provide an implementation that performs the sniff directly using <see cref="IRequestInvoker.Request{TResponse}"/> and the <see cref="BoundConfiguration"/>
	/// return by <see cref="CreateSniffEndpoint"/>
	/// </summary>
	public abstract Tuple<TransportResponse, IReadOnlyCollection<Node>> Sniff(IRequestInvoker requestInvoker, bool forceSsl, Endpoint endpoint, BoundConfiguration boundConfiguration);

	/// <summary> Allows certain nodes to be queried first to obtain sniffing information </summary>
	public abstract int SniffOrder(Node node);

	/// <summary> Predicate indicating a node is allowed to be used for API calls</summary>
	/// <param name="node">The node to inspect</param>
	/// <returns>bool, true if node should allows API calls</returns>
	public abstract bool NodePredicate(Node node);

	/// <summary>
	/// Used by the <see cref="ResponseFactory"/> to determine if it needs to return true or false for
	/// <see cref="ApiCallDetails.HasSuccessfulStatusCode"/>
	/// </summary>
	public abstract bool HttpStatusCodeClassifier(HttpMethod method, int statusCode);

	/// <summary>
	/// Try to obtain a server error from the response, this is used for debugging and exception messages
	/// </summary>
	public abstract bool TryGetServerErrorReason<TResponse>(TResponse response, out string reason) where TResponse : TransportResponse;

	/// <summary>
	/// Allows product implementations to inject a metadata header to all outgoing requests.
	/// </summary>
	public abstract MetaHeaderProvider MetaHeaderProvider { get; }

	/// <summary>
	/// The assembly informational version of the product.
	/// </summary>
	public abstract string ProductAssemblyVersion { get; }

	/// <summary>
	/// A set of common OpenTelemetry attributes for this product which are added to the logical operation span created
	/// by Elastic.Transport.
	/// </summary>
	public abstract IReadOnlyDictionary<string, object>? DefaultOpenTelemetryAttributes { get; }

	/// <summary>
	/// Returns a collection of header names to be parsed from the HTTP response.
	/// </summary>
	public abstract IReadOnlyCollection<string> DefaultHeadersToParse();

	/// <summary>
	/// May return a dictionary containing OpenTelemetry attributes parsed from the <see cref="ApiCallDetails"/> which are
	/// added to the logical operation span created by Elastic.Transport.
	/// </summary>
	public abstract Dictionary<string, object>? ParseOpenTelemetryAttributesFromApiCallDetails(ApiCallDetails callDetails);

	/// <summary>
	/// 
	/// </summary>
	public virtual IReadOnlyCollection<IResponseBuilder> ResponseBuilders { get; } = [new DefaultResponseBuilder()];
}
