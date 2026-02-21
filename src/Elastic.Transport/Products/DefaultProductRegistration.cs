// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Elastic.Transport.Products;

/// <summary>
/// A default non-descriptive product registration that does not support sniffing and pinging.
/// Can be used to connect to unknown services before they develop their own <see cref="ProductRegistration"/>
/// implementations
/// </summary>
public sealed class DefaultProductRegistration : ProductRegistration
{
	private readonly HeadersList _headers;
	private readonly MetaHeaderProvider _metaHeaderProvider;

	/// <summary>
	///
	/// </summary>
	public DefaultProductRegistration()
	{
		_headers = new();
		_metaHeaderProvider = new DefaultMetaHeaderProvider(typeof(ITransport), ServiceIdentifier ?? "et");

		ProductAssemblyVersion = typeof(ProductRegistration).Assembly
			.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
			.InformationalVersion ?? "unknown";
	}

	/// <summary> A static instance of <see cref="DefaultProductRegistration"/> to promote reuse </summary>
	public static DefaultProductRegistration Default { get; } = new();

	/// <inheritdoc cref="ProductRegistration.Name"/>
	public override string Name => "elastic-transport-net";

	/// <inheritdoc cref="ProductRegistration.ServiceIdentifier"/>
	public override string? ServiceIdentifier => "et";

	/// <inheritdoc cref="ProductRegistration.SupportsPing"/>
	public override bool SupportsPing => false;

	/// <inheritdoc cref="ProductRegistration.SupportsSniff"/>
	public override bool SupportsSniff => false;

	/// <inheritdoc cref="ProductRegistration.SniffOrder"/>
	public override int SniffOrder(Node node) => -1;

	/// <inheritdoc cref="ProductRegistration.NodePredicate"/>
	public override bool NodePredicate(Node node) => true;

	/// <inheritdoc cref="ProductRegistration.ResponseHeadersToParse"/>
	public override HeadersList ResponseHeadersToParse => _headers;

	/// <inheritdoc cref="ProductRegistration.MetaHeaderProvider"/>
	public override MetaHeaderProvider MetaHeaderProvider => _metaHeaderProvider;

	/// <inheritdoc cref="ProductRegistration.DefaultContentType"/>
	public override string? DefaultContentType => null;

	/// <inheritdoc cref="ProductRegistration.ProductAssemblyVersion"/>
	public override string ProductAssemblyVersion { get; }

	/// <inheritdoc cref="ProductRegistration.DefaultOpenTelemetryAttributes"/>
	public override IReadOnlyDictionary<string, object>? DefaultOpenTelemetryAttributes { get; }

	/// <inheritdoc cref="ProductRegistration.HttpStatusCodeClassifier"/>
	public override bool HttpStatusCodeClassifier(HttpMethod method, int statusCode) =>
		statusCode is >= 200 and < 300;

	/// <inheritdoc cref="ProductRegistration.TryGetServerErrorReason{TResponse}"/>>
	public override bool TryGetServerErrorReason<TResponse>(TResponse response, out string? reason)
	{
		reason = null;
		return false;
	}

	/// <inheritdoc cref="ProductRegistration.CreateSniffEndpoint"/>
	public override Endpoint CreateSniffEndpoint(Node node, IRequestConfiguration requestConfiguration, ITransportConfiguration settings) =>
		throw new NotImplementedException();

	/// <inheritdoc cref="ProductRegistration.SniffAsync"/>
	public override Task<Tuple<TransportResponse, IReadOnlyCollection<Node>>> SniffAsync(IRequestInvoker requestInvoker, bool forceSsl, Endpoint endpoint, BoundConfiguration boundConfiguration, CancellationToken cancellationToken) =>
		throw new NotImplementedException();

	/// <inheritdoc cref="ProductRegistration.Sniff"/>
	public override Tuple<TransportResponse, IReadOnlyCollection<Node>> Sniff(IRequestInvoker requestInvoker, bool forceSsl, Endpoint endpoint, BoundConfiguration boundConfiguration) =>
		throw new NotImplementedException();

	/// <inheritdoc cref="ProductRegistration.CreatePingEndpoint"/>
	public override Endpoint CreatePingEndpoint(Node node, IRequestConfiguration requestConfiguration) =>
		throw new NotImplementedException();

	/// <inheritdoc cref="ProductRegistration.PingAsync"/>
	public override Task<TransportResponse> PingAsync(IRequestInvoker requestInvoker, Endpoint endpoint, BoundConfiguration boundConfiguration, CancellationToken cancellationToken) =>
		throw new NotImplementedException();

	/// <inheritdoc cref="ProductRegistration.Ping"/>
	public override TransportResponse Ping(IRequestInvoker requestInvoker, Endpoint endpoint, BoundConfiguration boundConfiguration) =>
		throw new NotImplementedException();

	/// <inheritdoc/>
	public override IReadOnlyCollection<string> DefaultHeadersToParse() => [];

	/// <inheritdoc/>
	public override Dictionary<string, object>? ParseOpenTelemetryAttributesFromApiCallDetails(ApiCallDetails callDetails) => null;
}
