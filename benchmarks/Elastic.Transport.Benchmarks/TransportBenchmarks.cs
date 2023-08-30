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
		private DistributedTransport _requestHandler;

		[GlobalSetup]
		public void Setup()
		{
			var connection = new InMemoryRequestInvoker();
			var pool = new SingleNodePool(new Uri("http://localhost:9200"));
			var settings = new TransportConfiguration(pool, connection);

			_requestHandler = new DistributedTransport(settings);
		}

		[Benchmark]
		public void TransportSuccessfulRequestBenchmark() => _requestHandler.Get<VoidResponse>("/");

		[Benchmark]
		public async Task TransportSuccessfulAsyncRequestBenchmark() => await _requestHandler.GetAsync<VoidResponse>("/");
	}
}
