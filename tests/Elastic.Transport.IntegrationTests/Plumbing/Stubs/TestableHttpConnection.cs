// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Elastic.Transport.IntegrationTests.Plumbing.Stubs;

public class TestableHttpConnection : IRequestInvoker
{
	private readonly Action<HttpResponseMessage> _response;
	private TestableClientHandler _handler;
	public int CallCount { get; private set; }
	public HttpClientHandler LastHttpClientHandler => (HttpClientHandler)_handler.InnerHandler;
	private readonly HttpRequestInvoker _requestInvoker;

	public TestableHttpConnection(Action<HttpResponseMessage> response) : this() => _response = response;

	public TestableHttpConnection() =>
		_requestInvoker = new HttpRequestInvoker((defaultHandler, _) =>
		{
			_handler = new TestableClientHandler(defaultHandler, _response);
			return _handler;
		});

	public TResponse Request<TResponse>(RequestData requestData)
		where TResponse : TransportResponse, new()
	{
		CallCount++;
		return _requestInvoker.Request<TResponse>(requestData);
	}

	public Task<TResponse> RequestAsync<TResponse>(RequestData requestData, CancellationToken cancellationToken)
		where TResponse : TransportResponse, new()
	{
		CallCount++;
		return _requestInvoker.RequestAsync<TResponse>(requestData, cancellationToken);
	}

	public void Dispose()
	{
		_handler?.Dispose();
		_requestInvoker?.Dispose();
	}
}
