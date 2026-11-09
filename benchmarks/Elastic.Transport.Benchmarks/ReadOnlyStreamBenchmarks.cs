// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

#if NET11_0_OR_GREATER
using System.IO;
using System.Linq;
using System.Text;
using BenchmarkDotNet.Attributes;

namespace Elastic.Transport.Benchmarks;

/// <summary>
/// Reading data we already hold through a stream, which is what deserializing a string or replaying a response body does:
/// the way it was done (a factory stream over freshly encoded bytes) against the .NET 11 streams.
/// </summary>
public class ReadOnlyStreamBenchmarks
{
	private static readonly string Text = "{\"items\":[" + string.Join(",", Enumerable.Repeat("{\"name\":\"héllo wörld\",\"n\":12345}", 250)) + "]}";
	private static readonly byte[] Bytes = Encoding.UTF8.GetBytes(Text);

	[Benchmark(Baseline = true)]
	public long StringViaDefaultFactory()
	{
		using var stream = DefaultMemoryStreamFactory.Default.Create(Encoding.UTF8.GetBytes(Text));
		return Drain(stream);
	}

	[Benchmark]
	public long StringViaPooledFactory()
	{
		using var stream = RecyclableMemoryStreamFactory.Default.Create(Encoding.UTF8.GetBytes(Text));
		return Drain(stream);
	}

	[Benchmark]
	public long StringViaStringStream()
	{
		using var stream = new StringStream(Text, Encoding.UTF8);
		return Drain(stream);
	}

	[Benchmark]
	public long BytesViaPooledFactory()
	{
		using var stream = RecyclableMemoryStreamFactory.Default.Create(Bytes);
		return Drain(stream);
	}

	[Benchmark]
	public long BytesViaReadOnlyMemoryStream()
	{
		using var stream = new ReadOnlyMemoryStream(Bytes);
		return Drain(stream);
	}

	private static readonly byte[] Buffer = new byte[16 * 1024];

	// Count by reading: not every stream here supports Length, and reading is what a deserializer does.
	private static long Drain(Stream stream)
	{
		long total = 0;
		int read;
		while ((read = stream.Read(Buffer, 0, Buffer.Length)) > 0)
			total += read;
		return total;
	}
}
#endif
