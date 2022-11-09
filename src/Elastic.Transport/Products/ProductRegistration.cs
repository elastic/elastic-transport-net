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
	/// A default non-descriptive product registration that does not support sniffing and pinging.
	/// Can be used to connect to unknown services before they develop their own <see cref="IProductRegistration"/>
	/// implementations
	/// </summary>
	public sealed class ProductRegistration : IProductRegistration
	{
		private readonly HeadersList _headers = new();
		private readonly MetaHeaderProvider _metaHeaderProvider;

		/// <summary>
		/// 
		/// </summary>
		public ProductRegistration() => _metaHeaderProvider = new DefaultMetaHeaderProvider(typeof(HttpTransport), "et");

		/// <summary> A static instance of <see cref="ProductRegistration"/> to promote reuse </summary>
		public static ProductRegistration Default { get; } = new ProductRegistration();

		/// <inheritdoc cref="IProductRegistration.Name"/>
		public string Name { get; } = "elastic-transport-net";

		/// <inheritdoc cref="IProductRegistration.SupportsPing"/>
		public bool SupportsPing { get; } = false;

		/// <inheritdoc cref="IProductRegistration.SupportsSniff"/>
		public bool SupportsSniff { get; } = false;

		/// <inheritdoc cref="IProductRegistration.SniffOrder"/>
		public int SniffOrder(Node node) => -1;

		/// <inheritdoc cref="IProductRegistration.NodePredicate"/>
		public bool NodePredicate(Node node) => true;

		/// <inheritdoc cref="IProductRegistration.ResponseHeadersToParse"/>
		public HeadersList ResponseHeadersToParse => _headers;

		/// <inheritdoc cref="IProductRegistration.MetaHeaderProvider"/>
		public MetaHeaderProvider MetaHeaderProvider => _metaHeaderProvider;

		/// <inheritdoc cref="IProductRegistration.ResponseBuilder"/>
		public ResponseBuilder ResponseBuilder => new DefaultResponseBuilder<EmptyError>();

		/// <inheritdoc cref="IProductRegistration.HttpStatusCodeClassifier"/>
		public bool HttpStatusCodeClassifier(HttpMethod method, int statusCode) =>
			statusCode >= 200 && statusCode < 300;

		/// <inheritdoc cref="IProductRegistration.TryGetServerErrorReason{TResponse}"/>>
		public bool TryGetServerErrorReason<TResponse>(TResponse response, out string reason)
			where TResponse : TransportResponse
		{
			reason = null;
			return false;
		}

		/// <inheritdoc cref="IProductRegistration.CreateSniffRequestData"/>
		public RequestData CreateSniffRequestData(Node node, IRequestConfiguration requestConfiguration, ITransportConfiguration settings, MemoryStreamFactory memoryStreamFactory) =>
			throw new NotImplementedException();

		/// <inheritdoc cref="IProductRegistration.SniffAsync"/>
		public Task<Tuple<TransportResponse, IReadOnlyCollection<Node>>> SniffAsync(TransportClient transportClient, bool forceSsl, RequestData requestData, CancellationToken cancellationToken) =>
			throw new NotImplementedException();

		/// <inheritdoc cref="IProductRegistration.Sniff"/>
		public Tuple<TransportResponse, IReadOnlyCollection<Node>> Sniff(TransportClient connection, bool forceSsl, RequestData requestData) =>
			throw new NotImplementedException();

		/// <inheritdoc cref="IProductRegistration.CreatePingRequestData"/>
		public RequestData CreatePingRequestData(Node node, RequestConfiguration requestConfiguration, ITransportConfiguration global, MemoryStreamFactory memoryStreamFactory) =>
			throw new NotImplementedException();

		/// <inheritdoc cref="IProductRegistration.PingAsync"/>
		public Task<TransportResponse> PingAsync(TransportClient connection, RequestData pingData, CancellationToken cancellationToken) =>
			throw new NotImplementedException();

		/// <inheritdoc cref="IProductRegistration.Ping"/>
		public TransportResponse Ping(TransportClient connection, RequestData pingData) =>
			throw new NotImplementedException();
	}
}
