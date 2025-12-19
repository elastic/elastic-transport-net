// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.Transport.IntegrationTests.Plumbing;
using Elastic.Transport.IntegrationTests.Plumbing.Examples;
using Xunit;

[assembly: CaptureConsole, AssemblyFixture(typeof(TestServerFixture))]
[assembly: AssemblyFixture(typeof(BufferedServerFixture))]

namespace Elastic.Transport.IntegrationTests.Plumbing;

public class AssemblyServerTestsBase<TServer>(TServer instance)
	: IClassFixture<TServer> where TServer : class, IHttpTransportTestServer
{
	protected TServer Server { get; } = instance;

	protected ITransport RequestHandler => Server.DefaultRequestHandler;
}

public class AssemblyServerTestsBase(TestServerFixture instance)
	: AssemblyServerTestsBase<TestServerFixture>(instance);
