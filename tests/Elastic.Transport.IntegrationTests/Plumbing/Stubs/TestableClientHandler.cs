/*
 * Licensed to Elasticsearch B.V. under one or more contributor
 * license agreements. See the NOTICE file distributed with
 * this work for additional information regarding copyright
 * ownership. Elasticsearch B.V. licenses this file to you under
 * the Apache License, Version 2.0 (the "License"); you may
 * not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *    http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing,
 * software distributed under the License is distributed on an
 * "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
 * KIND, either express or implied.  See the License for the
 * specific language governing permissions and limitations
 * under the License.
 */

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Elastic.Transport.IntegrationTests.Plumbing.Stubs
{
	public class TestableClientHandler : DelegatingHandler
	{
		private readonly Action<HttpResponseMessage> _responseAction;

		public TestableClientHandler(HttpMessageHandler handler, Action<HttpResponseMessage> responseAction) : base(handler) =>
			_responseAction = responseAction;

		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
			_responseAction?.Invoke(response);
			return response;
		}
	}
}
