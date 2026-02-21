// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Elastic.Transport.IntegrationTests.Plumbing.Stubs;

public class TestableClientHandler(HttpMessageHandler handler, Action<HttpResponseMessage> responseAction) : DelegatingHandler(handler)
{
	private readonly Action<HttpResponseMessage> _responseAction = responseAction;

	protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
	{
		var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
		_responseAction?.Invoke(response);
		return response;
	}
}
