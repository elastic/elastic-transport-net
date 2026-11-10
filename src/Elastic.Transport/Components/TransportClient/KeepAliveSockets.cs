// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

#if NET
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Elastic.Transport;

/// <summary>
/// Opens the connections of <see cref="SocketsHttpHandler"/> with TCP keep-alive switched on. <c>SocketsHttpHandler</c> has no
/// keep-alive setting, so the transport connects the socket itself, as the .NET documentation shows for
/// <see cref="SocketsHttpHandler.ConnectCallback"/>. Everything else, TLS included, stays with the handler.
/// </summary>
internal static class KeepAliveSockets
{
	/// <summary>Unanswered probes after which the connection is dropped.</summary>
	internal const int ProbeCount = 5;

	internal static async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext context, TimeSpan keepAliveTime, TimeSpan keepAliveInterval, CancellationToken cancellationToken)
	{
		var socket = await ConnectSocketAsync(context.DnsEndPoint, keepAliveTime, keepAliveInterval, cancellationToken).ConfigureAwait(false);
		return new NetworkStream(socket, ownsSocket: true);
	}

	internal static async ValueTask<Socket> ConnectSocketAsync(DnsEndPoint endPoint, TimeSpan keepAliveTime, TimeSpan keepAliveInterval, CancellationToken cancellationToken)
	{
		// the same socket SocketsHttpHandler opens for itself
		var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
		try
		{
			Configure(socket, keepAliveTime, keepAliveInterval);
			await socket.ConnectAsync(endPoint, cancellationToken).ConfigureAwait(false);
			return socket;
		}
		catch
		{
			socket.Dispose();
			throw;
		}
	}

	internal static void Configure(Socket socket, TimeSpan keepAliveTime, TimeSpan keepAliveInterval)
	{
		socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

		// Each tuning option is best effort: an older Windows or an unusual platform may refuse one, and a connection without it
		// still has the operating system's keep-alive.
		TrySet(socket, SocketOptionName.TcpKeepAliveTime, Seconds(keepAliveTime));
		TrySet(socket, SocketOptionName.TcpKeepAliveInterval, Seconds(keepAliveInterval));
		TrySet(socket, SocketOptionName.TcpKeepAliveRetryCount, ProbeCount);
	}

	private static int Seconds(TimeSpan value) => (int)Math.Max(1, Math.Ceiling(value.TotalSeconds));

	private static void TrySet(Socket socket, SocketOptionName option, int value)
	{
		try
		{
			socket.SetSocketOption(SocketOptionLevel.Tcp, option, value);
		}
		catch (Exception e) when (e is SocketException or PlatformNotSupportedException or ArgumentException)
		{
		}
	}
}
#endif
