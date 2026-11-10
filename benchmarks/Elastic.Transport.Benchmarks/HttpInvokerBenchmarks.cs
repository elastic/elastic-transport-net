// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;

namespace Elastic.Transport.Benchmarks;

/// <summary>
/// The real <see cref="HttpRequestInvoker"/> against a minimal loopback server that answers every request with a canned
/// 200. The server reuses its buffers, so what is measured is almost entirely the client side. Used to decide whether
/// an HTTP-path change earns its keep: compare allocated bytes before and after.
/// </summary>
public class HttpInvokerBenchmarks : IDisposable
{
	private static readonly byte[] Response = Encoding.ASCII.GetBytes(
		"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: 2\r\n\r\n{}");

	private static readonly byte[] CannedBody = Encoding.UTF8.GetBytes("{\"query\":{\"match_all\":{}}}");

	// A realistic bulk-ish payload: big enough that the request body spills past one pooled block.
	private static readonly byte[] LargeBody = Encoding.UTF8.GetBytes("{\"doc\":\"" + new string('x', 64 * 1024) + "\"}");

	private readonly CancellationTokenSource _stop = new();
	private TcpListener _listener = null!;
	private ITransport _plain = null!;
	private ITransport _withAuthAndHeaders = null!;
	private ITransport _pooled = null!;
	private static readonly RequestConfiguration NoCompression = new() { EnableHttpCompression = false };
	private static readonly RequestConfiguration Chunked = new() { TransferEncodingChunked = true, EnableHttpCompression = false };
	private static readonly RequestConfiguration ChunkedCompressed = new() { TransferEncodingChunked = true };
	private static readonly RequestConfiguration NoDirectStreaming = new() { DisableDirectStreaming = true };

	[GlobalSetup]
	public void Setup()
	{
		_listener = new TcpListener(IPAddress.Loopback, 0);
		_listener.Start();
		var port = ((IPEndPoint)_listener.LocalEndpoint).Port;
		_ = Task.Run(AcceptLoop);

		var uri = new Uri($"http://127.0.0.1:{port}");
		_plain = new DistributedTransport(new TransportConfiguration(uri));
		_pooled = new DistributedTransport(new TransportConfiguration(uri) { MemoryStreamFactory = new RecyclableMemoryStreamFactory(blockSize: 16 * 1024, maximumFreeBytes: 50L * 1024 * 1024) });
		_withAuthAndHeaders = new DistributedTransport(new TransportConfiguration(uri) { Authentication = new BasicAuthentication("u", "p") });
	}

	private async Task AcceptLoop()
	{
		while (!_stop.IsCancellationRequested)
		{
			try
			{
				var client = await _listener.AcceptTcpClientAsync(_stop.Token).ConfigureAwait(false);
				_ = Task.Run(() => Serve(client));
			}
			catch (OperationCanceledException) { return; }
			catch (ObjectDisposedException) { return; }
		}
	}

	private static void Track(byte[] last, byte[] buffer, int offset, int count)
	{
		for (var i = Math.Max(0, count - 5); i < count; i++)
		{
			Array.Copy(last, 1, last, 0, 4);
			last[4] = buffer[offset + i];
		}
	}

	private static bool IsTerminator(byte[] last) => last.AsSpan().SequenceEqual("0\r\n\r\n"u8);

	private static async Task Serve(TcpClient client)
	{
		using var _ = client;
		var stream = client.GetStream();
		var buffer = new byte[16 * 1024];
		try
		{
			while (true)
			{
				// Read until the end of the headers; the bodies in these benchmarks are tiny and arrive with them.
				var read = 0;
				while (true)
				{
					var n = await stream.ReadAsync(buffer.AsMemory(read)).ConfigureAwait(false);
					if (n == 0) return;
					read += n;
					if (read >= 4 && buffer.AsSpan(0, read).IndexOf("\r\n\r\n"u8) >= 0) break;
				}
				// Decode only the headers, never the body bytes that may share the buffer: decoding them made this server
				// allocate tens of KB per request, which MemoryDiagnoser counts against the client.
				var headerEnd = buffer.AsSpan(0, read).IndexOf("\r\n\r\n"u8) + 4;
				var headers = Encoding.ASCII.GetString(buffer, 0, headerEnd);
				if (headers.Contains("Transfer-Encoding: chunked", StringComparison.OrdinalIgnoreCase))
				{
					// Read until the terminating zero-length chunk. The bodies here never contain that byte sequence.
					var last = new byte[5];
					Track(last, buffer, headerEnd, read - headerEnd);
					while (!IsTerminator(last))
					{
						var n = await stream.ReadAsync(buffer.AsMemory(0)).ConfigureAwait(false);
						if (n == 0) return;
						Track(last, buffer, 0, n);
					}
					await stream.WriteAsync(Response).ConfigureAwait(false);
					continue;
				}

				var marker = headers.IndexOf("Content-Length: ", StringComparison.OrdinalIgnoreCase);
				if (marker >= 0)
				{
					var end = headers.IndexOf("\r\n", marker, StringComparison.Ordinal);
					var length = int.Parse(headers.AsSpan(marker + 16, end - marker - 16));
					var have = read - headerEnd;
					while (have < length)
					{
						var n = await stream.ReadAsync(buffer.AsMemory(0)).ConfigureAwait(false);
						if (n == 0) return;
						have += n;
					}
				}
				await stream.WriteAsync(Response).ConfigureAwait(false);
			}
		}
		catch (Exception) { /* client went away */ }
	}

	[Benchmark(Baseline = true)]
	public Task<VoidResponse> Get() => _plain.RequestAsync<VoidResponse>(new EndpointPath(Elastic.Transport.HttpMethod.GET, "/"), null, null, null);

	[Benchmark]
	public Task<VoidResponse> GetWithAuthentication() =>
		_withAuthAndHeaders.RequestAsync<VoidResponse>(new EndpointPath(Elastic.Transport.HttpMethod.GET, "/"), null, null, null);

	[Benchmark]
	public Task<VoidResponse> PostSmallBody() =>
		_plain.RequestAsync<VoidResponse>(new EndpointPath(Elastic.Transport.HttpMethod.POST, "/_search"), PostData.Bytes(CannedBody), null, null);

	/// <summary>The same body without request compression, to separate the gzip step from our own handling.</summary>
	[Benchmark]
	public Task<VoidResponse> PostSmallBodyNoCompression() =>
		_plain.RequestAsync<VoidResponse>(new EndpointPath(Elastic.Transport.HttpMethod.POST, "/_search"), PostData.Bytes(CannedBody), null,
			NoCompression);

	[Benchmark]
	public Task<VoidResponse> PostLargeBodyNoCompression() =>
		_plain.RequestAsync<VoidResponse>(new EndpointPath(Elastic.Transport.HttpMethod.POST, "/_bulk"), PostData.Bytes(LargeBody), null,
			NoCompression);

	/// <summary>Chunked transfer encoding (RequestDataContent), uncompressed.</summary>
	[Benchmark]
	public Task<VoidResponse> PostLargeBodyChunked() =>
		_plain.RequestAsync<VoidResponse>(new EndpointPath(Elastic.Transport.HttpMethod.POST, "/_bulk"), PostData.Bytes(LargeBody), null, Chunked);

	[Benchmark]
	public Task<VoidResponse> PostLargeBodyChunkedCompressed() =>
		_plain.RequestAsync<VoidResponse>(new EndpointPath(Elastic.Transport.HttpMethod.POST, "/_bulk"), PostData.Bytes(LargeBody), null, ChunkedCompressed);

	private static readonly object LargeObject = new { doc = new string('x', 64 * 1024), tags = new[] { "a", "b", "c" } };

	/// <summary>The common real body: an object the serializer writes into a stream (so the stream factory matters).</summary>
	[Benchmark]
	public Task<VoidResponse> PostLargeSerializableNoCompression() =>
		_plain.RequestAsync<VoidResponse>(new EndpointPath(Elastic.Transport.HttpMethod.POST, "/_bulk"), PostData.Serializable(LargeObject), null, NoCompression);

	[Benchmark]
	public Task<VoidResponse> PostLargeSerializable() =>
		_plain.RequestAsync<VoidResponse>(new EndpointPath(Elastic.Transport.HttpMethod.POST, "/_bulk"), PostData.Serializable(LargeObject), null, null);

	/// <summary>The same two bodies through an opt-in pool bounded to 50 MB idle (16 KB blocks).</summary>
	[Benchmark]
	public Task<VoidResponse> PostLargeSerializableNoCompressionBoundedPool() =>
		_pooled.RequestAsync<VoidResponse>(new EndpointPath(Elastic.Transport.HttpMethod.POST, "/_bulk"), PostData.Serializable(LargeObject), null, NoCompression);

	[Benchmark]
	public Task<VoidResponse> PostSmallBodyDirectStreamingOffBoundedPool() =>
		_pooled.RequestAsync<VoidResponse>(new EndpointPath(Elastic.Transport.HttpMethod.POST, "/_search"), PostData.Bytes(CannedBody), null, NoDirectStreaming);

	[Benchmark]
	public Task<VoidResponse> PostLargeBody() =>
		_plain.RequestAsync<VoidResponse>(new EndpointPath(Elastic.Transport.HttpMethod.POST, "/_bulk"), PostData.Bytes(LargeBody), null, null);

	[Benchmark]
	public Task<VoidResponse> PostSmallBodyDirectStreamingOff() =>
		_plain.RequestAsync<VoidResponse>(new EndpointPath(Elastic.Transport.HttpMethod.POST, "/_search"), PostData.Bytes(CannedBody), null,
			NoDirectStreaming);

	public void Dispose()
	{
		_stop.Cancel();
		_listener.Stop();
		_stop.Dispose();
		GC.SuppressFinalize(this);
	}

	[GlobalCleanup]
	public void Cleanup() => Dispose();
}
