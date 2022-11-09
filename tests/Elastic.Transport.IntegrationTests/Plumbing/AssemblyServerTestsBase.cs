// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Xunit.Extensions.Ordering;

namespace Elastic.Transport.IntegrationTests.Plumbing
{
	public class AssemblyServerTestsBase<TServer> : IAssemblyFixture<TServer> where TServer : class, HttpTransportTestServer
	{
		public AssemblyServerTestsBase(TServer instance) => Server = instance;

		protected TServer Server { get; }

		protected HttpTransport Transport => Server.DefaultTransport;
	}

	public class AssemblyServerTestsBase : AssemblyServerTestsBase<TransportTestServer>
	{
		public AssemblyServerTestsBase(TransportTestServer instance) : base(instance) { }
	}
}
