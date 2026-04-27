// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Elastic.Transport.Diagnostics;

namespace Elastic.Transport.Products.Elasticsearch;

/// <summary>
/// An implementation of <see cref="ProductRegistration"/> that fills in the bespoke implementations
/// for Elasticsearch so that <see cref="RequestPipeline"/> knows how to ping and sniff if we setup
/// <see cref="ITransport{TConfiguration}"/> to talk to Elasticsearch
/// </summary>
public partial class ElasticsearchProductRegistration : ProductRegistration
{
	internal const string XFoundHandlingClusterHeader = "X-Found-Handling-Cluster";
	internal const string XFoundHandlingInstanceHeader = "X-Found-Handling-Instance";

#if NET7_0_OR_GREATER
	[GeneratedRegex(@"application/vnd\.elasticsearch\+(json|x-ndjson|vnd\.mapbox-vector-tile)", RegexOptions.IgnoreCase)]
	private static partial Regex VendorMimeRegex();

	private static readonly Regex _vendorMimeRegex = VendorMimeRegex();
#else
	private static readonly Regex _vendorMimeRegex = new(
		@"application/vnd\.elasticsearch\+(json|x-ndjson|vnd\.mapbox-vector-tile)",
		RegexOptions.Compiled | RegexOptions.IgnoreCase);
#endif

	private readonly int? _clientMajorVersion;
	private readonly ConcurrentDictionary<string, string>? _contentTypeCache;

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
		{
			_clientMajorVersion = clientVersionInfo.Major;
			_contentTypeCache = new ConcurrentDictionary<string, string>(StringComparer.Ordinal);
		}

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
	/// <remarks>
	/// Always returns the bare vendor MIME type. The <c>;compatible-with=N</c>
	/// suffix is appended by <see cref="TransformContentType"/> when the value is
	/// bound onto a request — but only when a client major version is known, so
	/// the suffix is omitted for the parameterless registration.
	/// </remarks>
	public override string? DefaultContentType => "application/vnd.elasticsearch+json";

	/// <inheritdoc cref="ProductRegistration.TransformContentType"/>
	/// <remarks>
	/// Appends <c>;compatible-with=N</c> to a supported vendor MIME type
	/// (<c>application/vnd.elasticsearch+json</c>, <c>+x-ndjson</c>, or
	/// <c>+vnd.mapbox-vector-tile</c>) when the parameter is not already present.
	/// Plain MIME types like <c>application/json</c> are returned unchanged so the
	/// caller stays in control of the value they explicitly provided.
	/// </remarks>
	public override string? TransformContentType(string? contentType)
	{
		if (string.IsNullOrEmpty(contentType) || !_clientMajorVersion.HasValue)
			return contentType;

		return _contentTypeCache!.GetOrAdd(contentType!, AppendCompatibleWithAnnotation);
	}

	private string AppendCompatibleWithAnnotation(string input)
	{
		// If a compatible-with parameter is already present anywhere in the
		// value, treat it as user-controlled and leave it alone.
		if (input.IndexOf("compatible-with=", StringComparison.OrdinalIgnoreCase) >= 0)
			return input;

		// If no supported vendor MIME type is present, nothing to do.
		if (!_vendorMimeRegex.IsMatch(input))
			return input;

		return _vendorMimeRegex.Replace(input, $"$0;compatible-with={_clientMajorVersion!.Value}");
	}

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
	/// The 404 case is handled on a per-request basis (see <see cref="IsValidResponse(ApiCallDetails)"/> for details).
	/// </remarks>
	public override bool HttpStatusCodeClassifier(HttpMethod method, int statusCode) =>
		statusCode is >= 200 and < 300;

	/// <inheritdoc cref="ProductRegistration.IsValidResponse(ApiCallDetails)"/>
	/// <remarks>
	/// A response is valid when:
	/// <list type="bullet">
	/// <item>The content-type matches what the caller asked for.</item>
	/// <item>For 404 responses: there is no extracted Elasticsearch server error
	/// (404s without an error body — e.g. <c>GET /my-index/_doc/missing-doc-id</c> — represent
	/// a legitimate "missing entity" rather than a failure).</item>
	/// <item>Otherwise: the status code is in the success range (or explicitly allowed via
	/// <see cref="IRequestConfiguration.AllowedStatusCodes"/>).</item>
	/// </list>
	/// </remarks>
	public override bool IsValidResponse(ApiCallDetails? details)
	{
		if (details is null || !details.HasExpectedContentType)
			return false;

		var serverError = details.ProductError as ElasticsearchServerError;
		if (details.HttpStatusCode is 404)
			return !serverError?.HasError() ?? true;

		return details.HasSuccessfulStatusCode;
	}

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
