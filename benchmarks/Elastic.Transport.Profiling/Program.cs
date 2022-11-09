// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Threading.Tasks;
using Elastic.Transport.Products.Elasticsearch;
using JetBrains.Profiler.Api;

namespace Elastic.Transport.Profiling
{
	internal class Program
	{
		private static async Task Main()
		{
			MemoryProfiler.CollectAllocations(true);
			MemoryProfiler.GetSnapshot("start");

			var config = new TransportConfiguration(new Uri("http://localhost:9200"), new ElasticsearchProductRegistration());
			var transport = new DefaultHttpTransport(config);

			_ = await transport.GetAsync<VoidResponse>("/");

			MemoryProfiler.GetSnapshot("before-many-requests");

			for (var i = 0; i < 1_000; i++) _ = await transport.GetAsync<VoidResponse>("/");

			MemoryProfiler.GetSnapshot("after-many-requests");
			//MeasureProfiler.StopCollectingData();

			await Task.Delay(1000);

			MemoryProfiler.ForceGc();
			MemoryProfiler.GetSnapshot("before-final-request");
			_ = await transport.GetAsync<VoidResponse>("/");
			MemoryProfiler.GetSnapshot("after-final-request");

			MemoryProfiler.ForceGc();
			MemoryProfiler.GetSnapshot("end");
		}
	}
}
