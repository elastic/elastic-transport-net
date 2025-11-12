// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Xunit;

namespace Elastic.Transport.IntegrationTests.Plumbing
{
	public class ClassServerTestsBase<TServer>(TServer instance)
		where TServer : class, IHttpTransportTestServer
	{
		protected TServer Server { get; } = instance;

		protected ITransport RequestHandler => Server.DefaultRequestHandler;
	}
}
