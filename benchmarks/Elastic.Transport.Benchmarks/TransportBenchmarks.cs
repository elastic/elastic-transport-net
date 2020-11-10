using BenchmarkDotNet.Attributes;
using Elastic.Transport.VirtualizedCluster;

namespace Elastic.Transport.Benchmarks
{
	[MemoryDiagnoser]
	public class TransportBenchmarks
	{
		private static readonly VirtualizedCluster.Components.VirtualizedCluster ClusterWithSuccess = Virtual.Elasticsearch
			.Bootstrap(1)
			.ClientCalls(c => c.SucceedAlways())
			.StaticConnectionPool()
			.Settings(s => s.DisablePing());

		[Benchmark]
		public void TransportSuccessfulRequestBenchmark() => ClusterWithSuccess.Transport.Get<R>("/");

		private class R : ITransportResponse
		{
			public IApiCallDetails ApiCall { get; set; }
		}
	}
}
