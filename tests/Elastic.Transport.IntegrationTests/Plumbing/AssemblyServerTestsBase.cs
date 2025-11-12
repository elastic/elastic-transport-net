// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Nullean.Xunit.Partitions.Sdk;
using Xunit;

namespace Elastic.Transport.IntegrationTests.Plumbing
{
	public class AssemblyServerTestsBase<TServer>(TServer instance)
		: IPartitionFixture<TServer>, IClassFixture<TServer> where TServer : class, HttpTransportTestServer, IPartitionLifetime
	{
		protected TServer Server { get; } = instance;

		protected ITransport RequestHandler => Server.DefaultRequestHandler;
	}

	public class AssemblyServerTestsBase(TransportTestServer instance)
		: AssemblyServerTestsBase<TransportTestServer>(instance);
}
