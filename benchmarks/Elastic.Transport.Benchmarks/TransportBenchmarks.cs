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
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;

namespace Elastic.Transport.Benchmarks
{
	public class TransportBenchmarks
	{
		private Transport _transport;

		[GlobalSetup]
		public void Setup()
		{
			var connection = new InMemoryConnection();
			var pool = new SingleNodeConnectionPool(new Uri("http://localhost:9200"));
			var settings = new TransportConfiguration(pool, connection);

			_transport = new Transport(settings);
		}

		[Benchmark]
		public void TransportSuccessfulRequestBenchmark() => _transport.Get<EmptyResponse>("/");

		[Benchmark]
		public async Task TransportSuccessfulAsyncRequestBenchmark() => await _transport.GetAsync<EmptyResponse>("/");

		private class EmptyResponse : ITransportResponse
		{
			public IApiCallDetails ApiCall { get; set; }
		}
	}
}
