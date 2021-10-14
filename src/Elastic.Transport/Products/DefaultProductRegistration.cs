// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Elastic.Transport.Products
{

	/// <summary>
	/// A default non-descriptive product registration that does not support sniffing and pinging.
	/// Can be used to connect to unknown services before they develop their own <see cref="IProductRegistration"/>
	/// implementations
	/// </summary>
	public class ProductRegistration : IProductRegistration
	{
		private readonly HeadersList _headers = new();

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

		/// <inheritdoc cref="IProductRegistration.HttpStatusCodeClassifier"/>
		public bool HttpStatusCodeClassifier(HttpMethod method, int statusCode) =>
			statusCode >= 200 && statusCode < 300;

		/// <inheritdoc cref="IProductRegistration.TryGetServerErrorReason{TResponse}"/>>
		public bool TryGetServerErrorReason<TResponse>(TResponse response, out string reason)
			where TResponse : ITransportResponse
		{
			reason = null;
			return false;
		}

		/// <inheritdoc cref="IProductRegistration.CreateSniffRequestData"/>
		public RequestData CreateSniffRequestData(Node node, IRequestConfiguration requestConfiguration, ITransportConfiguration settings, IMemoryStreamFactory memoryStreamFactory) =>
			throw new NotImplementedException();

		/// <inheritdoc cref="IProductRegistration.SniffAsync"/>
		public Task<Tuple<IApiCallDetails, IReadOnlyCollection<Node>>> SniffAsync(IConnection connection, bool forceSsl, RequestData requestData, CancellationToken cancellationToken) =>
			throw new NotImplementedException();

		/// <inheritdoc cref="IProductRegistration.Sniff"/>
		public Tuple<IApiCallDetails, IReadOnlyCollection<Node>> Sniff(IConnection connection, bool forceSsl, RequestData requestData) =>
			throw new NotImplementedException();

		/// <inheritdoc cref="IProductRegistration.CreatePingRequestData"/>
		public RequestData CreatePingRequestData(Node node, RequestConfiguration requestConfiguration, ITransportConfiguration global, IMemoryStreamFactory memoryStreamFactory) =>
			throw new NotImplementedException();

		/// <inheritdoc cref="IProductRegistration.PingAsync"/>
		public Task<IApiCallDetails> PingAsync(IConnection connection, RequestData pingData, CancellationToken cancellationToken) =>
			throw new NotImplementedException();

		/// <inheritdoc cref="IProductRegistration.Ping"/>
		public IApiCallDetails Ping(IConnection connection, RequestData pingData) =>
			throw new NotImplementedException();
	}
}
