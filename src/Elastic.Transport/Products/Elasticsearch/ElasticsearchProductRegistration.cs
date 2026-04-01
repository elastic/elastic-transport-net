// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Elastic.Transport.Diagnostics;

namespace Elastic.Transport.Products.Elasticsearch;

/// <summary>
/// An implementation of <see cref="ProductRegistration"/> that fills in the bespoke implementations
/// for Elasticsearch so that <see cref="RequestPipeline"/> knows how to ping and sniff if we setup
/// <see cref="ITransport{TConfiguration}"/> to talk to Elasticsearch
/// </summary>
public class ElasticsearchProductRegistration : ProductRegistration
{
	internal const string XFoundHandlingClusterHeader = "X-Found-Handling-Cluster";
	internal const string XFoundHandlingInstanceHeader = "X-Found-Handling-Instance";

	private readonly int? _clientMajorVersion;

	private static string? _clusterName;
	private static readonly string[] _all = [XFoundHandlingClusterHeader, XFoundHandlingInstanceHeader];
	private static readonly string[] _instanceHeader = [XFoundHandlingInstanceHeader];

	/// <summary>
	/// Create a new instance of the Elasticsearch product registration.
	/// </summary>
	internal ElasticsearchProductRegistration()
	{
		ResponseHeadersToParse = new HeadersList("warning");
		MetaHeaderProvider = null!;
		ProductAssemblyVersion = null!;
	}

	/// <summary>
	///
	/// </summary>
	/// <param name="markerType"></param>
	public ElasticsearchProductRegistration(Type markerType) : this()
	{
		var clientVersionInfo = ReflectionVersionInfo.Create(markerType);

		var identifier = ServiceIdentifier;
		if (!string.IsNullOrEmpty(identifier))
			MetaHeaderProvider = new DefaultMetaHeaderProvider(clientVersionInfo, identifier!);

		// Only set this if we have a version.
		// If we don't have a version we won't apply the vendor-based REST API compatibility Accept header.
		if (clientVersionInfo.Major > 0)
			_clientMajorVersion = clientVersionInfo.Major;

		ProductAssemblyVersion = markerType.Assembly
			.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
			?.InformationalVersion ?? string.Empty;
	}

	/// <summary> A static instance of <see cref="ElasticsearchProductRegistration"/> to promote reuse </summary>
	public static ProductRegistration Default { get; } = new ElasticsearchProductRegistration();

	/// <inheritdoc cref="ProductRegistration.Name"/>
	public override string Name { get; } = "elasticsearch-net";

	/// <inheritdoc cref="ProductRegistration.ServiceIdentifier"/>
	public sealed override string ServiceIdentifier => "es";

	/// <inheritdoc cref="ProductRegistration.SupportsPing"/>
	public override bool SupportsPing { get; } = true;

	/// <inheritdoc cref="ProductRegistration.SupportsSniff"/>
	public override bool SupportsSniff { get; } = true;

	/// <inheritdoc cref="ProductRegistration.ResponseHeadersToParse"/>
	public override HeadersList ResponseHeadersToParse { get; }

	/// <inheritdoc cref="ProductRegistration.MetaHeaderProvider"/>
	public override MetaHeaderProvider MetaHeaderProvider { get; }

	/// <inheritdoc cref="ProductRegistration.DefaultContentType"/>
	public override string? DefaultContentType => _clientMajorVersion.HasValue ? $"application/vnd.elasticsearch+json;compatible-with={_clientMajorVersion.Value}" : null;

	/// <summary> Exposes the path used for sniffing in Elasticsearch </summary>
	public const string SniffPath = "_nodes/http,settings";

	/// <summary>
	/// Implements an ordering that prefers master eligible nodes when attempting to sniff the
	/// <see cref="NodePool.Nodes"/>
	/// </summary>
	public override int SniffOrder(Node node) =>
		node.HasFeature(ElasticsearchNodeFeatures.MasterEligible) ? node.Uri.Port : int.MaxValue;

	/// <summary>
	/// If we know that a node is a master eligible node that hold no data it is excluded from regular
	/// API calls. They are considered for ping and sniff requests.
	/// </summary>
	public override bool NodePredicate(Node node) =>
		// Skip master only nodes (holds no data and is master eligible)
		!(node.HasFeature(ElasticsearchNodeFeatures.MasterEligible) &&
		  !node.HasFeature(ElasticsearchNodeFeatures.HoldsData));

	/// <inheritdoc cref="ProductRegistration.HttpStatusCodeClassifier"/>
	/// <remarks>
	/// We consider all status codes &gt;= 200 and &lt; 300 valid by default.
	/// Elasticsearch might return 404 for valid responses in some cases (e.g. `GET /my-index/_doc/missing-doc-id`) but also for actual error cases like
	/// missing endpoints, missing indices (e.g. `GET /missing-index/_mapping`), etc.
	/// The 404 case is handled on a per-request basis (see <see cref="ElasticsearchResponseHelper.IsValidResponse"/> for details).
	/// </remarks>
	public override bool HttpStatusCodeClassifier(HttpMethod method, int statusCode) =>
		statusCode is >= 200 and < 300;

	/// <inheritdoc cref="ProductRegistration.TryGetServerErrorReason{TResponse}"/>>
	public override bool TryGetServerErrorReason<TResponse>(TResponse response, out string? reason)
	{
		reason = null;

		if (response.ApiCallDetails?.ProductError is ElasticsearchServerError error && error.HasError())
		{
			reason = error.Error?.ToString();
			return true;
		}

		return false;
	}

	//TODO remove settings dependency
	/// <inheritdoc cref="ProductRegistration.CreateSniffEndpoint"/>
	public override Endpoint CreateSniffEndpoint(Node node, IRequestConfiguration requestConfiguration, ITransportConfiguration settings)
	{
		var requestParameters = new DefaultRequestParameters();
		if (requestConfiguration.PingTimeout.HasValue)
			requestParameters.QueryString.Add("timeout", requestConfiguration.PingTimeout.Value);
		requestParameters.QueryString.Add("flat_settings", true);
		var sniffPath = requestParameters.CreatePathWithQueryStrings(SniffPath, settings);
		return new Endpoint(new EndpointPath(HttpMethod.GET, sniffPath), node);
	}

	/// <inheritdoc cref="ProductRegistration.SniffAsync"/>
	public override async Task<Tuple<TransportResponse, IReadOnlyCollection<Node>>> SniffAsync(IRequestInvoker requestInvoker,
		bool forceSsl, Endpoint endpoint, BoundConfiguration boundConfiguration, CancellationToken cancellationToken)
	{
		var response = await requestInvoker.RequestAsync<SniffResponse>(endpoint, boundConfiguration, null, cancellationToken)
			.ConfigureAwait(false);
		var nodes = response.ToNodes(forceSsl);
		return Tuple.Create<TransportResponse, IReadOnlyCollection<Node>>(response,
			new ReadOnlyCollection<Node>(nodes.ToArray()));
	}

	/// <inheritdoc cref="ProductRegistration.Sniff"/>
	public override Tuple<TransportResponse, IReadOnlyCollection<Node>> Sniff(IRequestInvoker requestInvoker, bool forceSsl,
		Endpoint endpoint, BoundConfiguration boundConfiguration)
	{
		var response = requestInvoker.Request<SniffResponse>(endpoint, boundConfiguration, null);
		var nodes = response.ToNodes(forceSsl);
		return Tuple.Create<TransportResponse, IReadOnlyCollection<Node>>(response,
			new ReadOnlyCollection<Node>(nodes.ToArray()));
	}

	/// <inheritdoc cref="ProductRegistration.CreatePingEndpoint"/>
	public override Endpoint CreatePingEndpoint(Node node, IRequestConfiguration requestConfiguration) =>
		new(new EndpointPath(HttpMethod.HEAD, string.Empty), node);

	/// <inheritdoc cref="ProductRegistration.PingAsync"/>
	public override async Task<TransportResponse> PingAsync(IRequestInvoker requestInvoker, Endpoint endpoint, BoundConfiguration boundConfiguration, CancellationToken cancellationToken)
	{
		var response = await requestInvoker.RequestAsync<VoidResponse>(endpoint, boundConfiguration, null, cancellationToken).ConfigureAwait(false);
		return response;
	}

	/// <inheritdoc cref="ProductRegistration.Ping"/>
	public override TransportResponse Ping(IRequestInvoker requestInvoker, Endpoint endpoint, BoundConfiguration boundConfiguration)
	{
		var response = requestInvoker.Request<VoidResponse>(endpoint, boundConfiguration, null);
		return response;
	}

	/// <inheritdoc/>
	public override IReadOnlyCollection<string> DefaultHeadersToParse()
	{
		if (OpenTelemetry.CurrentSpanIsElasticTransportOwnedAndHasListeners && (Activity.Current?.IsAllDataRequested ?? false))
		{
			if (string.IsNullOrEmpty(_clusterName))
				return _all;
			else
				return _instanceHeader;
		}

		return [];
	}

	/// <inheritdoc/>
	public override Dictionary<string, object>? ParseOpenTelemetryAttributesFromApiCallDetails(ApiCallDetails callDetails)
	{
		Dictionary<string, object>? attributes = null;

		if (string.IsNullOrEmpty(_clusterName) && callDetails.TryGetHeader(XFoundHandlingClusterHeader, out var clusterValues))
		{
			_clusterName = clusterValues.FirstOrDefault();
		}

		if (!string.IsNullOrEmpty(_clusterName))
		{
			attributes ??= [];
			attributes.Add(OpenTelemetryAttributes.DbElasticsearchClusterName, _clusterName!);
		}

		if (callDetails.TryGetHeader(XFoundHandlingInstanceHeader, out var instanceValues))
		{
			var instance = instanceValues.FirstOrDefault();
			if (!string.IsNullOrEmpty(instance))
			{
				attributes ??= [];
				attributes.Add(OpenTelemetryAttributes.DbElasticsearchNodeName, instance);
			}
		}

		return attributes;
	}

	/// <inheritdoc/>
	public override string ProductAssemblyVersion { get; }

	/// <inheritdoc/>
	public override IReadOnlyDictionary<string, object>? DefaultOpenTelemetryAttributes { get; } = new Dictionary<string, object>
	{
		[SemanticConventions.DbSystem] = "elasticsearch"
	};

	/// <inheritdoc/>
	public override IReadOnlyCollection<IResponseBuilder> ResponseBuilders { get; } =
	[
		new StringResponseBuilder<ElasticsearchStringResponse>(),
		new DynamicResponseBuilder<ElasticsearchDynamicResponse>(),
		new JsonResponseBuilder<ElasticsearchJsonResponse>(),
		new ElasticsearchStreamResponseBuilder(),
#if NET10_0_OR_GREATER
		new ElasticsearchPipeResponseBuilder(),
#endif
		new ElasticsearchResponseBuilder()
	];

	/// <inheritdoc />
	public override bool IsErrorContentType(string? contentType) =>
		contentType is not null && (
			contentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase) ||
			contentType.StartsWith("application/vnd.elasticsearch+json", StringComparison.OrdinalIgnoreCase));

	/// <inheritdoc />
	public override ErrorResponse? TryExtractError(BoundConfiguration boundConfiguration, Stream responseStream)
	{
		try
		{
			var error = boundConfiguration.ConnectionSettings.RequestResponseSerializer
				.Deserialize<ElasticsearchServerError>(responseStream);

			if (error?.HasError() == true)
				return error;
		}
		catch (System.Text.Json.JsonException)
		{
			// If the error deserialization fails, we'll let the builder try the original response type.
		}

		return null;
	}
}
