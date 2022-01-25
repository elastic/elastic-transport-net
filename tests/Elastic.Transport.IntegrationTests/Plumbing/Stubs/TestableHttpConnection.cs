// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Elastic.Transport.IntegrationTests.Plumbing.Stubs
{
	public class TestableHttpConnection : HttpTransportClient
	{
		private readonly Action<HttpResponseMessage> _response;
		private TestableClientHandler _handler;
		public int CallCount { get; private set; }
		public HttpClientHandler LastHttpClientHandler => (HttpClientHandler)_handler.InnerHandler;

		public TestableHttpConnection(Action<HttpResponseMessage> response) => _response = response;

		public TestableHttpConnection() { }

		public override TResponse Request<TResponse>(RequestData requestData)
		{
			CallCount++;
			return base.Request<TResponse>(requestData);
		}

		public override Task<TResponse> RequestAsync<TResponse>(RequestData requestData, CancellationToken cancellationToken)
		{
			CallCount++;
			return base.RequestAsync<TResponse>(requestData, cancellationToken);
		}

		protected override HttpMessageHandler CreateHttpClientHandler(RequestData requestData)
		{
			_handler = new TestableClientHandler(base.CreateHttpClientHandler(requestData), _response);
			return _handler;
		}

		protected override void DisposeManagedResources()
		{
			_handler?.Dispose();
			base.DisposeManagedResources();
		}
	}
}
