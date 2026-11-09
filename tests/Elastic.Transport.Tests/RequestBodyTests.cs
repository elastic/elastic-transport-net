// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

// HttpRequestInvoker (the HttpClient path) does not exist on .NET Framework.
#if !NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace Elastic.Transport.Tests;

/// <summary>What a request body looks like on the wire, read by a real socket.</summary>
public sealed class RequestBodyTests : IDisposable
{
	private readonly TcpListener _listener = new(IPAddress.Loopback, 0);

	public RequestBodyTests() => _listener.Start();

	public void Dispose() => _listener.Stop();

	private sealed record Received(Dictionary<string, string> Headers, byte[] Body);

	/// <summary>Serve exactly one request and report what arrived.</summary>
	private Task<Received> ServeOnce() => Task.Run(async () =>
	{
		using var client = await _listener.AcceptTcpClientAsync();
		var stream = client.GetStream();
		var head = new List<byte>();
		var one = new byte[1];
		while (!(head.Count >= 4 && head[^4] == '\r' && head[^3] == '\n' && head[^2] == '\r' && head[^1] == '\n'))
		{
			if (await stream.ReadAsync(one) == 0) break;
			head.Add(one[0]);
		}

		var lines = Encoding.ASCII.GetString(head.ToArray()).Split(["\r\n"], StringSplitOptions.RemoveEmptyEntries);
		var headers = lines.Skip(1).Select(l => l.Split([": "], 2, StringSplitOptions.None))
			.ToDictionary(p => p[0], p => p[1], StringComparer.OrdinalIgnoreCase);

		var body = new byte[headers.TryGetValue("Content-Length", out var length) ? int.Parse(length) : 0];
		var read = 0;
		while (read < body.Length)
		{
			var n = await stream.ReadAsync(body.AsMemory(read));
			if (n == 0) break;
			read += n;
		}

		await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: 2\r\n\r\n{}"));
		return new Received(headers, body);
	});

	private async Task<Received> Post(PostData body, bool compress)
	{
		var received = ServeOnce();
		var port = ((IPEndPoint)_listener.LocalEndpoint).Port;
		using var settings = new TransportConfiguration(new Uri($"http://127.0.0.1:{port}")) { EnableHttpCompression = compress };
		var transport = new DistributedTransport(settings);

		_ = await transport.RequestAsync<VoidResponse>(new EndpointPath(HttpMethod.POST, "/_bulk"), body, null, null);
		return await received;
	}

	private static byte[] Gunzip(byte[] bytes)
	{
		using var input = new GZipStream(new MemoryStream(bytes), CompressionMode.Decompress);
		using var output = new MemoryStream();
		input.CopyTo(output);
		return output.ToArray();
	}

	[Fact]
	public async Task UncompressedBytesAreSentAsTheyAre()
	{
		// Larger than any single buffer on the way, so a body cut short or sent twice would show.
		var payload = Encoding.UTF8.GetBytes("{\"doc\":\"" + new string('x', 100_000) + "\"}");

		var received = await Post(PostData.Bytes(payload), compress: false);

		_ = received.Body.Should().Equal(payload);
		_ = received.Headers["Content-Length"].Should().Be(payload.Length.ToString());
		_ = received.Headers["Content-Type"].Should().Be("application/json");
		_ = received.Headers.Should().NotContainKey("Content-Encoding");
	}

	[Fact]
	public async Task CompressedBytesAreStillGzipped()
	{
		var payload = Encoding.UTF8.GetBytes("{\"doc\":\"" + new string('x', 5_000) + "\"}");

		var received = await Post(PostData.Bytes(payload), compress: true);

		_ = received.Headers["Content-Encoding"].Should().Be("gzip");
		_ = Gunzip(received.Body).Should().Equal(payload);
		_ = received.Headers["Content-Type"].Should().Be("application/json");
	}

	[Fact]
	public async Task OtherBodyTypesAreUnaffected()
	{
		var received = await Post(PostData.String("{\"a\":1}"), compress: false);

		_ = Encoding.UTF8.GetString(received.Body).Should().Be("{\"a\":1}");
		_ = received.Headers["Content-Type"].Should().Be("application/json");
	}
}
#endif
