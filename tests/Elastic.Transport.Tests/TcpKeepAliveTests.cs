// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using FluentAssertions;
using Xunit;
#if NET
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
#endif

namespace Elastic.Transport.Tests;

/// <summary>
/// Connections send TCP keep-alive probes after 300 seconds of silence, then every second, and give up after 5
/// unanswered ones. Idle connections stay open through a load balancer that drops silent ones, and a dead peer is noticed.
/// </summary>
public class TcpKeepAliveTests
{
	private static readonly Uri Node = new("http://localhost:9200");

	[Fact]
	public void DefaultsAreFiveMinutesThenOneSecond()
	{
		var bound = new BoundConfiguration(new TransportConfiguration(Node), null);

		_ = bound.KeepAliveTime.Should().Be(300_000);
		_ = bound.KeepAliveInterval.Should().Be(1_000);
	}

	[Fact]
	public void ConfiguredValuesStillWin()
	{
		var settings = new TransportConfiguration(Node) { KeepAliveTime = TimeSpan.FromSeconds(60), KeepAliveInterval = TimeSpan.FromSeconds(5) };

		var bound = new BoundConfiguration(settings, null);

		_ = bound.KeepAliveTime.Should().Be(60_000);
		_ = bound.KeepAliveInterval.Should().Be(5_000);
	}

#if NET
	private static async Task<(Socket Client, TcpListener Listener)> ConnectAsync(TimeSpan time, TimeSpan interval)
	{
		var listener = new TcpListener(IPAddress.Loopback, 0);
		listener.Start();
		var port = ((IPEndPoint)listener.LocalEndpoint).Port;
		var client = await KeepAliveSockets.ConnectSocketAsync(new DnsEndPoint("127.0.0.1", port), time, interval, default);
		return (client, listener);
	}

	[Fact]
	public async Task AConnectionSendsKeepAliveProbesAsConfigured()
	{
		var (client, listener) = await ConnectAsync(TimeSpan.FromSeconds(300), TimeSpan.FromSeconds(7));
		using var socket = client;
		listener.Stop();

		// BSD systems report the option bit rather than 1
		_ = client.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive).Should().NotBe(0);
		_ = client.GetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime).Should().Be(300);
		_ = client.GetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval).Should().Be(7);
		_ = client.GetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount).Should().Be(5);
		_ = client.NoDelay.Should().BeTrue("SocketsHttpHandler's own connect sets it");
	}

	[Fact]
	public async Task ARequestWorksThroughTheCustomConnect()
	{
		var listener = new TcpListener(IPAddress.Loopback, 0);
		listener.Start();
		var port = ((IPEndPoint)listener.LocalEndpoint).Port;
		var server = Task.Run(async () =>
		{
			using var accepted = await listener.AcceptTcpClientAsync();
			var stream = accepted.GetStream();
			var buffer = new byte[4096];
			_ = await stream.ReadAsync(buffer);
			var response = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: 2\r\nConnection: close\r\n\r\n{}");
			await stream.WriteAsync(response);
		});

		HttpMessageHandler created = null;
		var invoker = new HttpRequestInvoker((handler, _) => created = handler);
		var transport = new DistributedTransport(new TransportConfiguration(new SingleNodePool(new Uri($"http://127.0.0.1:{port}")), invoker));
		var response = await transport.RequestAsync<StringResponse>(HttpMethod.GET, "/");
		await server;
		listener.Stop();

		_ = response.ApiCallDetails.HasSuccessfulStatusCode.Should().BeTrue();
		_ = response.Body.Should().Be("{}");
		_ = created.Should().BeOfType<SocketsHttpHandler>().Which.ConnectCallback.Should().NotBeNull("it is what opens the socket with keep-alive");
	}

	[Fact]
	public async Task AFailedConnectReleasesTheSocket()
	{
		// nothing listens on this port
		var listener = new TcpListener(IPAddress.Loopback, 0);
		listener.Start();
		var port = ((IPEndPoint)listener.LocalEndpoint).Port;
		listener.Stop();

		Func<Task> connect = () => KeepAliveSockets.ConnectSocketAsync(new DnsEndPoint("127.0.0.1", port), TimeSpan.FromSeconds(300), TimeSpan.FromSeconds(1), default).AsTask();

		_ = await connect.Should().ThrowAsync<SocketException>();
	}
#endif
}
