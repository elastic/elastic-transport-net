// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using CsCheck;
using FluentAssertions;
using Xunit;

namespace Elastic.Transport.Properties.Tests;

/// <summary>
/// A request body is written by Write or by WriteAsync, with or without buffering for diagnostics, and for six kinds of body.
/// All of those must produce the same bytes, and the buffered copy must be the bytes that were sent.
/// </summary>
public class PostDataProperties
{
	private static readonly TransportConfiguration Settings = new(new Uri("http://localhost:9200"));

	private static readonly Gen<string> Text =
		Gen.Int[0x20, 0xD7FF].Select(i => (char)i).Array[0, 60].Select(c => new string(c));

	private sealed record Body(string Kind, PostData Data, byte[] Expected);

	private static readonly Gen<Body> Bodies = Gen.OneOf(
		Gen.Byte.Array[0, 400].Select(b => new Body("bytes", PostData.Bytes(b), b)),
		Gen.Byte.Array[0, 400].Select(b => new Body("read-only memory", PostData.ReadOnlyMemory(b), b)),
		Text.Select(t => new Body("string", PostData.String(t), Encoding.UTF8.GetBytes(t))),
		Text.Select(t => { var node = new JsonObject { ["v"] = t }; return new Body("serializable", PostData.Serializable(node), Encoding.UTF8.GetBytes(node.ToJsonString())); }),
		Text.Array[0, 5].Select(lines => new Body("multi json", PostData.MultiJson(lines), Encoding.UTF8.GetBytes(string.Concat(lines.Select(l => l + "\n"))))));

	private static readonly bool[] BufferModes = [false, true];

	private static byte[] Sync(PostData data, bool buffer)
	{
		using var stream = new MemoryStream();
		data.Write(stream, Settings, buffer);
		return stream.ToArray();
	}

	private static byte[] Async(PostData data, bool buffer)
	{
		using var stream = new MemoryStream();
		data.WriteAsync(stream, Settings, buffer, CancellationToken.None).GetAwaiter().GetResult();
		return stream.ToArray();
	}

	[Fact]
	public void EveryBodyWritesTheSameBytesSyncOrAsyncBufferedOrNot() =>
		Bodies.Sample(body =>
		{
			foreach (var buffer in BufferModes)
			{
				_ = Sync(body.Data, buffer).Should().Equal(body.Expected, $"{body.Kind}, sync, buffered={buffer}");
				_ = Async(body.Data, buffer).Should().Equal(body.Expected, $"{body.Kind}, async, buffered={buffer}");
			}
		}, iter: 600);

	[Fact]
	public void WhenBufferedTheBytesKeptForDiagnosticsAreTheBytesThatWereSent() =>
		Bodies.Sample(body =>
		{
			var sent = Sync(body.Data, buffer: true);

			// An empty body leaves WrittenBytes null (nothing was written), which reads the same as empty.
			_ = (body.Data.WrittenBytes ?? []).Should().Equal(sent, body.Kind);
		}, iter: 400);

	[Fact]
	public void ABodyCanBeWrittenAgainAndGivesTheSameBytes() =>
		Bodies.Sample(body =>
		{
			var first = Sync(body.Data, buffer: false);
			var second = Sync(body.Data, buffer: false);

			_ = second.Should().Equal(first, $"{body.Kind}: a retry sends the same body");
		}, iter: 300);
}
