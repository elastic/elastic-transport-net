// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;

namespace Elastic.Transport.Benchmarks;

/// <summary>The request-body steps of HttpRequestInvoker.SetContentAsync in isolation, with no sockets and no server.</summary>
public class BodyBenchmarks : System.IDisposable
{
	private static readonly byte[] LargeBody = Encoding.UTF8.GetBytes("{\"doc\":\"" + new string('x', 64 * 1024) + "\"}");
	private readonly TransportConfiguration _settings = new(new System.Uri("http://localhost:9200"));

	/// <summary>Default (plain) stream, write the bytes, rewind, send through StreamContent: what an uncompressed request does.</summary>
	[Benchmark(Baseline = true)]
	public async Task DefaultStreamThenStreamContent()
	{
		var stream = _settings.MemoryStreamFactory.Create();
		await PostData.Bytes(LargeBody).WriteAsync(stream, _settings, false, default).ConfigureAwait(false);
		stream.Position = 0;
		using var content = new StreamContent(stream);
		await content.CopyToAsync(Stream.Null).ConfigureAwait(false);
	}

	/// <summary>The default factory is a plain MemoryStream; this is the opt-in pooled one (the code under test in the RecyclableMemoryStream swap).</summary>
	[Benchmark]
	public async Task RecyclableStreamThenStreamContent()
	{
		var stream = RecyclableMemoryStreamFactory.Default.Create();
		await PostData.Bytes(LargeBody).WriteAsync(stream, _settings, false, default).ConfigureAwait(false);
		stream.Position = 0;
		using var content = new StreamContent(stream);
		await content.CopyToAsync(Stream.Null).ConfigureAwait(false);
	}

	/// <summary>Only the pooled stream and the write, to see whether the allocation is there.</summary>
	[Benchmark]
	public async Task DefaultStreamWriteOnly()
	{
		using var stream = _settings.MemoryStreamFactory.Create();
		await PostData.Bytes(LargeBody).WriteAsync(stream, _settings, false, default).ConfigureAwait(false);
	}

	/// <summary>The bytes are already in memory: send them without any copy.</summary>
	[Benchmark]
	public async Task ByteArrayContentDirect()
	{
		using var content = new ByteArrayContent(LargeBody);
		await content.CopyToAsync(Stream.Null).ConfigureAwait(false);
	}

	[GlobalCleanup]
	public void Cleanup() => Dispose();

	public void Dispose()
	{
		_settings.Dispose();
		System.GC.SuppressFinalize(this);
	}
}
