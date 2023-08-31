// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Elastic.Transport.Diagnostics;

namespace Elastic.Transport.Products.Elasticsearch;

/// <summary>
/// An implementation of <see cref="ProductRegistration"/> that fills in the bespoke implementations
/// for Elasticsearch so that <see cref="RequestPipeline"/> knows how to ping and sniff if we setup
/// <see cref="HttpTransport{TConnectionSettings}"/> to talk to Elasticsearch
/// </summary>
public class ElasticsearchProductRegistration : ProductRegistration
{
	private readonly HeadersList _headers;
	private readonly MetaHeaderProvider _metaHeaderProvider;
	private readonly int? _clientMajorVersion;

	private static string _clusterName;
	private static readonly string[] _all = new[] { "X-Found-Handling-Cluster", "X-Found-Handling-Instance" };
	private static readonly string[] _instanceHeader = new[] { "X-Found-Handling-Instance" };

	/// <summary>
	/// Create a new instance of the Elasticsearch product registration.
	/// </summary>
	public ElasticsearchProductRegistration() => _headers = new HeadersList("warning");

	/// <summary>
	/// 
	/// </summary>
	/// <param name="markerType"></param>
	public ElasticsearchProductRegistration(Type markerType) : this()
	{
		var clientVersionInfo = ReflectionVersionInfo.Create(markerType);

		var identifier = ServiceIdentifier;
		if (!string.IsNullOrEmpty(identifier))
			_metaHeaderProvider = new DefaultMetaHeaderProvider(clientVersionInfo, identifier);

		// Only set this if we have a version.
		// If we don't have a version we won't apply the vendor-based REST API compatibility Accept header.
		if (clientVersionInfo.Version.Major > 0)
			_clientMajorVersion = clientVersionInfo.Version.Major;

		ProductAssemblyVersion = markerType.Assembly
			.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
			.InformationalVersion;
	}

	/// <summary> A static instance of <see cref="ElasticsearchProductRegistration"/> to promote reuse </summary>
	public static ProductRegistration Default { get; } = new ElasticsearchProductRegistration();

	/// <inheritdoc cref="ProductRegistration.Name"/>
	public override string Name { get; } = "elasticsearch-net";

	/// <inheritdoc cref="ProductRegistration.ServiceIdentifier"/>
	public override string? ServiceIdentifier => "es";

	/// <inheritdoc cref="ProductRegistration.SupportsPing"/>
	public override bool SupportsPing { get; } = true;

	/// <inheritdoc cref="ProductRegistration.SupportsSniff"/>
	public override bool SupportsSniff { get; } = true;

	/// <inheritdoc cref="ProductRegistration.ResponseHeadersToParse"/>
	public override HeadersList ResponseHeadersToParse => _headers;

	/// <inheritdoc cref="ProductRegistration.MetaHeaderProvider"/>
	public override MetaHeaderProvider MetaHeaderProvider => _metaHeaderProvider;

	/// <inheritdoc cref="ProductRegistration.ResponseBuilder"/>
	public override ResponseBuilder ResponseBuilder => new ElasticsearchResponseBuilder();

	/// <inheritdoc cref="ProductRegistration.DefaultMimeType"/>
	public override string DefaultMimeType => _clientMajorVersion.HasValue ? $"application/vnd.elasticsearch+json;compatible-with={_clientMajorVersion.Value}" : null;

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
		// skip master only nodes (holds no data and is master eligible)
		!(node.HasFeature(ElasticsearchNodeFeatures.MasterEligible) &&
		  !node.HasFeature(ElasticsearchNodeFeatures.HoldsData));

	/// <inheritdoc cref="ProductRegistration.HttpStatusCodeClassifier"/>
	public override bool HttpStatusCodeClassifier(HttpMethod method, int statusCode) =>
		statusCode >= 200 && statusCode < 300;

	/// <inheritdoc cref="ProductRegistration.TryGetServerErrorReason{TResponse}"/>>
	public override bool TryGetServerErrorReason<TResponse>(TResponse response, out string reason)
	{
		reason = null;
		if (response is StringResponse s && s.TryGetElasticsearchServerError(out var e)) reason = e.Error?.ToString();
		else if (response is BytesResponse b && b.TryGetElasticsearchServerError(out e)) reason = e.Error?.ToString();
		else if (response.TryGetElasticsearchServerError(out e)) reason = e.Error?.ToString();
		return e != null;
	}

	/// <inheritdoc cref="ProductRegistration.CreateSniffRequestData"/>
	public override RequestData CreateSniffRequestData(Node node, IRequestConfiguration requestConfiguration,
		ITransportConfiguration settings,
		MemoryStreamFactory memoryStreamFactory
	)
	{
		var requestParameters = new DefaultRequestParameters
		{
			QueryString = {{"timeout", requestConfiguration.PingTimeout}, {"flat_settings", true},}
		};
		return new RequestData(HttpMethod.GET, SniffPath, null, settings, requestParameters, memoryStreamFactory, default)
		{
			Node = node
		};
	}

	/// <inheritdoc cref="ProductRegistration.SniffAsync"/>
	public override async Task<Tuple<TransportResponse, IReadOnlyCollection<Node>>> SniffAsync(TransportClient transportClient,
		bool forceSsl, RequestData requestData, CancellationToken cancellationToken)
	{
		var response = await transportClient.RequestAsync<SniffResponse>(requestData, cancellationToken)
			.ConfigureAwait(false);
		var nodes = response.ToNodes(forceSsl);
		return Tuple.Create<TransportResponse, IReadOnlyCollection<Node>>(response,
			new ReadOnlyCollection<Node>(nodes.ToArray()));
	}

	/// <inheritdoc cref="ProductRegistration.Sniff"/>
	public override Tuple<TransportResponse, IReadOnlyCollection<Node>> Sniff(TransportClient transportClient, bool forceSsl,
		RequestData requestData)
	{
		var response = transportClient.Request<SniffResponse>(requestData);
		var nodes = response.ToNodes(forceSsl);
		return Tuple.Create<TransportResponse, IReadOnlyCollection<Node>>(response,
			new ReadOnlyCollection<Node>(nodes.ToArray()));
	}

	/// <inheritdoc cref="ProductRegistration.CreatePingRequestData"/>
	public override RequestData CreatePingRequestData(Node node, RequestConfiguration requestConfiguration,
		ITransportConfiguration global,
		MemoryStreamFactory memoryStreamFactory
	)
	{
		var requestParameters = new DefaultRequestParameters
		{
			RequestConfiguration = requestConfiguration
		};

		var data = new RequestData(HttpMethod.HEAD, string.Empty, null, global, requestParameters, memoryStreamFactory, default)
		{
			Node = node
		};

		return data;
	}

	/// <inheritdoc cref="ProductRegistration.PingAsync"/>
	public override async Task<TransportResponse> PingAsync(TransportClient transportClient, RequestData pingData,
		CancellationToken cancellationToken)
	{
		var response = await transportClient.RequestAsync<VoidResponse>(pingData, cancellationToken).ConfigureAwait(false);
		return response;
	}

	/// <inheritdoc cref="ProductRegistration.Ping"/>
	public override TransportResponse Ping(TransportClient connection, RequestData pingData)
	{
		var response = connection.Request<VoidResponse>(pingData);
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

		return Array.Empty<string>();
	}

	/// <inheritdoc/>
	public override Dictionary<string, object>? ParseOpenTelemetryAttributesFromApiCallDetails(ApiCallDetails callDetails)
	{
		Dictionary<string, object>? attributes = null;

		if (string.IsNullOrEmpty(_clusterName) && callDetails.TryGetHeader("X-Found-Handling-Cluster", out var clusterValues))
		{
			_clusterName = clusterValues.FirstOrDefault();
		}

		if (!string.IsNullOrEmpty(_clusterName))
		{
			attributes ??= new Dictionary<string, object>();
			attributes.Add("db.elasticsearch.cluster.name", _clusterName);
		}

		if (callDetails.TryGetHeader("X-Found-Handling-Instance", out var instanceValues))
		{
			var instance = instanceValues.FirstOrDefault();
			if (!string.IsNullOrEmpty(instance))
			{
				attributes ??= new Dictionary<string, object>();
				attributes.Add("db.elasticsearch.node.name", instance);
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
}
