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
	private readonly HeadersList _headers = new();
	private readonly MetaHeaderProvider _metaHeaderProvider;

	/// <summary>
	/// 
	/// </summary>
	public DefaultProductRegistration()
	{
		_metaHeaderProvider = new DefaultMetaHeaderProvider(typeof(HttpTransport), ServiceIdentifier);

		ProductAssemblyVersion = typeof(ProductRegistration).Assembly
			.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
			.InformationalVersion;
	}

	/// <summary> A static instance of <see cref="DefaultProductRegistration"/> to promote reuse </summary>
	public static DefaultProductRegistration Default { get; } = new DefaultProductRegistration();

	/// <inheritdoc cref="ProductRegistration.Name"/>
	public override string Name { get; } = "elastic-transport-net";

	/// <inheritdoc cref="ProductRegistration.ServiceIdentifier"/>
	public override string? ServiceIdentifier => "et";

	/// <inheritdoc cref="ProductRegistration.SupportsPing"/>
	public override bool SupportsPing { get; } = false;

	/// <inheritdoc cref="ProductRegistration.SupportsSniff"/>
	public override bool SupportsSniff { get; } = false;

	/// <inheritdoc cref="ProductRegistration.SniffOrder"/>
	public override int SniffOrder(Node node) => -1;

	/// <inheritdoc cref="ProductRegistration.NodePredicate"/>
	public override bool NodePredicate(Node node) => true;

	/// <inheritdoc cref="ProductRegistration.ResponseHeadersToParse"/>
	public override HeadersList ResponseHeadersToParse => _headers;

	/// <inheritdoc cref="ProductRegistration.MetaHeaderProvider"/>
	public override MetaHeaderProvider MetaHeaderProvider => _metaHeaderProvider;

	/// <inheritdoc cref="ProductRegistration.ResponseBuilder"/>
	public override ResponseBuilder ResponseBuilder => new DefaultResponseBuilder<EmptyError>();

	/// <inheritdoc cref="ProductRegistration.DefaultMimeType"/>
	public override string DefaultMimeType => null;

	/// <inheritdoc cref="ProductRegistration.ProductAssemblyVersion"/>
	public override string ProductAssemblyVersion { get; }

	/// <inheritdoc cref="ProductRegistration.DefaultOpenTelemetryAttributes"/>
	public override IReadOnlyDictionary<string, object>? DefaultOpenTelemetryAttributes { get; }

	/// <inheritdoc cref="ProductRegistration.HttpStatusCodeClassifier"/>
	public override bool HttpStatusCodeClassifier(HttpMethod method, int statusCode) =>
		statusCode >= 200 && statusCode < 300;

	/// <inheritdoc cref="ProductRegistration.TryGetServerErrorReason{TResponse}"/>>
	public override bool TryGetServerErrorReason<TResponse>(TResponse response, out string reason)
	{
		reason = null;
		return false;
	}

	/// <inheritdoc cref="ProductRegistration.CreateSniffRequestData"/>
	public override RequestData CreateSniffRequestData(Node node, IRequestConfiguration requestConfiguration, ITransportConfiguration settings, MemoryStreamFactory memoryStreamFactory) =>
		throw new NotImplementedException();

	/// <inheritdoc cref="ProductRegistration.SniffAsync"/>
	public override Task<Tuple<TransportResponse, IReadOnlyCollection<Node>>> SniffAsync(TransportClient transportClient, bool forceSsl, RequestData requestData, CancellationToken cancellationToken) =>
		throw new NotImplementedException();

	/// <inheritdoc cref="ProductRegistration.Sniff"/>
	public override Tuple<TransportResponse, IReadOnlyCollection<Node>> Sniff(TransportClient connection, bool forceSsl, RequestData requestData) =>
		throw new NotImplementedException();

	/// <inheritdoc cref="ProductRegistration.CreatePingRequestData"/>
	public override RequestData CreatePingRequestData(Node node, RequestConfiguration requestConfiguration, ITransportConfiguration global, MemoryStreamFactory memoryStreamFactory) =>
		throw new NotImplementedException();

	/// <inheritdoc cref="ProductRegistration.PingAsync"/>
	public override Task<TransportResponse> PingAsync(TransportClient connection, RequestData pingData, CancellationToken cancellationToken) =>
		throw new NotImplementedException();

	/// <inheritdoc cref="ProductRegistration.Ping"/>
	public override TransportResponse Ping(TransportClient connection, RequestData pingData) =>
		throw new NotImplementedException();

	/// <inheritdoc/>
	public override IReadOnlyCollection<string> DefaultHeadersToParse() => Array.Empty<string>();

	/// <inheritdoc/>
	public override Dictionary<string, object>? ParseOpenTelemetryAttributesFromApiCallDetails(ApiCallDetails callDetails) => null;
}
