// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.IO;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;

namespace Elastic.Transport.Benchmarks;

/// <summary>
/// Whether a pooled stream earns its keep for small and medium serialized bodies, measured the way it matters: allocated
/// bytes and GCs per body, and throughput with many requests building bodies at once (no network, so nothing hides it).
/// A body written into a plain MemoryStream grows it by doubling, so even a few KB leaves several short-lived buffers behind.
/// </summary>
public class SmallBodyPoolBenchmarks : IDisposable
{
	private const int Threads = 16;
	private const int PerThread = 200;

	private TransportConfiguration _settings = null!;
	private PostData _body = null!;
	private readonly MemoryStreamFactory _plain = DefaultMemoryStreamFactory.Default;
	private readonly MemoryStreamFactory _pooled = new RecyclableMemoryStreamFactory(blockSize: 16 * 1024, maximumFreeBytes: 50L * 1024 * 1024);

	[Params(256, 1024, 4096, 16384)]
	public int Size { get; set; }

	[GlobalSetup]
	public void Setup()
	{
		_settings = new TransportConfiguration(new Uri("http://localhost:9200"));
		_body = PostData.Serializable(new { doc = new string('x', Size) });
	}

	private void Build(MemoryStreamFactory factory)
	{
		using var stream = factory.Create();
		_body.Write(stream, _settings, false);
	}

	[Benchmark(Baseline = true)]
	public void Plain() => Build(_plain);

	[Benchmark]
	public void Pooled() => Build(_pooled);

	[Benchmark(OperationsPerInvoke = Threads * PerThread)]
	public void PlainParallel() => Parallel.For(0, Threads, _ => { for (var i = 0; i < PerThread; i++) Build(_plain); });

	[Benchmark(OperationsPerInvoke = Threads * PerThread)]
	public void PooledParallel() => Parallel.For(0, Threads, _ => { for (var i = 0; i < PerThread; i++) Build(_pooled); });

	[GlobalCleanup]
	public void Cleanup() => Dispose();

	public void Dispose()
	{
		_settings.Dispose();
		GC.SuppressFinalize(this);
	}
}
