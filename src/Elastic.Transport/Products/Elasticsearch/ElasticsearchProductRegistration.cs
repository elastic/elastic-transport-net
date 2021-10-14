// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Elastic.Transport.Products.Elasticsearch.Sniff;

namespace Elastic.Transport.Products.Elasticsearch
{
	/// <summary>
	/// An implementation of <see cref="IProductRegistration"/> that fills in the bespoke implementations
	/// for Elasticsearch so that <see cref="IRequestPipeline"/> knows how to ping and sniff if we setup
	/// <see cref="ITransport{TConnectionSettings}"/> to talk to Elasticsearch
	/// </summary>
	public class ElasticsearchProductRegistration : IProductRegistration
	{
		private readonly HeadersList _headers;

		/// <summary>
		/// Create a new instance of the Elasticsearch product registration.
		/// </summary>
		public ElasticsearchProductRegistration()
		{
			_headers = new HeadersList();
			_headers.TryAdd("warning");
		}

		/// <summary> A static instance of <see cref="ElasticsearchProductRegistration"/> to promote reuse </summary>
		public static IProductRegistration Default { get; } = new ElasticsearchProductRegistration();

		/// <inheritdoc cref="IProductRegistration.Name"/>
		public string Name { get; } = "elasticsearch-net";

		/// <inheritdoc cref="IProductRegistration.SupportsPing"/>
		public bool SupportsPing { get; } = true;

		/// <inheritdoc cref="IProductRegistration.SupportsSniff"/>
		public bool SupportsSniff { get; } = true;

		/// <inheritdoc cref="IProductRegistration.ResponseHeadersToParse"/>
		public HeadersList ResponseHeadersToParse => _headers;

		/// <summary> Exposes the path used for sniffing in Elasticsearch </summary>
		public const string SniffPath = "_nodes/http,settings";

		/// <summary>
		/// Implements an ordering that prefers master eligible nodes when attempting to sniff the
		/// <see cref="IConnectionPool.Nodes"/>
		/// </summary>
		public int SniffOrder(Node node) =>
			node.HasFeature(ElasticsearchNodeFeatures.MasterEligible) ? node.Uri.Port : int.MaxValue;

		/// <summary>
		/// If we know that a node is a master eligible node that hold no data it is excluded from regular
		/// API calls. They are considered for ping and sniff requests.
		/// </summary>
		public bool NodePredicate(Node node) =>
			// skip master only nodes (holds no data and is master eligible)
			!(node.HasFeature(ElasticsearchNodeFeatures.MasterEligible) &&
			  !node.HasFeature(ElasticsearchNodeFeatures.HoldsData));

		/// <inheritdoc cref="IProductRegistration.HttpStatusCodeClassifier"/>
		public virtual bool HttpStatusCodeClassifier(HttpMethod method, int statusCode) =>
			statusCode >= 200 && statusCode < 300;

		/// <inheritdoc cref="IProductRegistration.TryGetServerErrorReason{TResponse}"/>>
		public virtual bool TryGetServerErrorReason<TResponse>(TResponse response, out string reason)
			where TResponse : ITransportResponse
		{
			reason = null;
			if (response is StringResponse s && s.TryGetElasticsearchServerError(out var e)) reason = e.Error?.ToString();
			else if (response is BytesResponse b && b.TryGetElasticsearchServerError(out e)) reason = e.Error?.ToString();
			else if (response.TryGetElasticsearchServerError(out e)) reason = e.Error?.ToString();
			return e != null;
		}

		/// <inheritdoc cref="IProductRegistration.CreateSniffRequestData"/>
		public RequestData CreateSniffRequestData(Node node, IRequestConfiguration requestConfiguration,
			ITransportConfiguration settings,
			IMemoryStreamFactory memoryStreamFactory
		)
		{
			var requestParameters = new RequestParameters
			{
				QueryString = {{"timeout", requestConfiguration.PingTimeout}, {"flat_settings", true},}
			};
			return new RequestData(HttpMethod.GET, SniffPath, null, settings, requestParameters, memoryStreamFactory)
			{
				Node = node
			};
		}

		/// <inheritdoc cref="IProductRegistration.SniffAsync"/>
		public async Task<Tuple<IApiCallDetails, IReadOnlyCollection<Node>>> SniffAsync(IConnection connection,
			bool forceSsl, RequestData requestData, CancellationToken cancellationToken)
		{
			var response = await connection.RequestAsync<SniffResponse>(requestData, cancellationToken)
				.ConfigureAwait(false);
			var nodes = response.ToNodes(forceSsl);
			return Tuple.Create<IApiCallDetails, IReadOnlyCollection<Node>>(response,
				new ReadOnlyCollection<Node>(nodes.ToArray()));
		}

		/// <inheritdoc cref="IProductRegistration.Sniff"/>
		public Tuple<IApiCallDetails, IReadOnlyCollection<Node>> Sniff(IConnection connection, bool forceSsl,
			RequestData requestData)
		{
			var response = connection.Request<SniffResponse>(requestData);
			var nodes = response.ToNodes(forceSsl);
			return Tuple.Create<IApiCallDetails, IReadOnlyCollection<Node>>(response,
				new ReadOnlyCollection<Node>(nodes.ToArray()));
		}

		/// <inheritdoc cref="IProductRegistration.CreatePingRequestData"/>
		public RequestData CreatePingRequestData(Node node, RequestConfiguration requestConfiguration,
			ITransportConfiguration global,
			IMemoryStreamFactory memoryStreamFactory
		)
		{
			IRequestParameters requestParameters = new RequestParameters
			{
				RequestConfiguration = requestConfiguration
			};

			var data = new RequestData(HttpMethod.HEAD, string.Empty, null, global, requestParameters,
				memoryStreamFactory) {Node = node};
			return data;
		}

		/// <inheritdoc cref="IProductRegistration.PingAsync"/>
		public async Task<IApiCallDetails> PingAsync(IConnection connection, RequestData pingData,
			CancellationToken cancellationToken) =>
			await connection.RequestAsync<VoidResponse>(pingData, cancellationToken).ConfigureAwait(false);

		/// <inheritdoc cref="IProductRegistration.Ping"/>
		public IApiCallDetails Ping(IConnection connection, RequestData pingData) =>
			connection.Request<VoidResponse>(pingData);
	}
}
