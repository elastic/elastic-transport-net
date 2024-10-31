// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;

namespace Elastic.Transport.Benchmarks
{
	public class TransportBenchmarks
	{
		private ITransport _transport;

		[GlobalSetup]
		public void Setup()
		{
			var requestInvoker = new InMemoryRequestInvoker();
			var pool = new SingleNodePool(new Uri("http://localhost:9200"));
			var settings = new TransportConfigurationDescriptor(pool, requestInvoker);

			_transport = new DistributedTransport(settings);
		}

		[Benchmark]
		public void TransportSuccessfulRequestBenchmark() => _transport.Get<VoidResponse>("/");

		[Benchmark]
		public async Task TransportSuccessfulAsyncRequestBenchmark() => await _transport.GetAsync<VoidResponse>("/");
	}
}
