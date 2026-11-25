// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Elastic.Transport.Tests.Plumbing;
using Elastic.Transport.Tests.SharedComponents;
using FluentAssertions;
using Xunit;

namespace Elastic.Transport.Tests;

/// <summary>
/// A stream that the transport takes from a <see cref="MemoryStreamFactory"/> goes back to it when the request throws,
/// as it does when the request succeeds. A pooled factory only gets its blocks back on dispose.
/// </summary>
public class DisposeOnErrorTests
{
	private static readonly byte[] MalformedJson = Encoding.UTF8.GetBytes("{ this is not json");

	private static (TransportConfiguration, TrackingMemoryStreamFactory) Configure(bool disableDirectStreaming)
	{
		var factory = new TrackingMemoryStreamFactory();
		var config = InMemoryConnectionFactory.Create() with
		{
			DisableDirectStreaming = disableDirectStreaming,
			MemoryStreamFactory = factory
		};
		return (config, factory);
	}

	private static Endpoint EndpointFor() =>
		new(new EndpointPath(HttpMethod.GET, "/"), new Node(new Uri("http://localhost:9200")));

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task DeserializationFailureDisposesBufferedStream(bool async)
	{
		var (config, factory) = Configure(disableDirectStreaming: true);
		var bound = new BoundConfiguration(config, null);
		var stream = new TrackDisposeStream();
		stream.Write(MalformedJson, 0, MalformedJson.Length);
		stream.Position = 0;

		Func<Task> call = async () =>
		{
			if (async)
				_ = await config.RequestInvoker.ResponseFactory.CreateAsync<TestResponse>(EndpointFor(), bound, null, null, 200, null, stream, "application/json", -1, null, null);
			else
				_ = config.RequestInvoker.ResponseFactory.Create<TestResponse>(EndpointFor(), bound, null, null, 200, null, stream, "application/json", -1, null, null);
		};

		_ = await call.Should().ThrowAsync<JsonException>();

		_ = factory.Created.Should().NotBeEmpty();
		_ = factory.Created.All(s => s.IsDisposed).Should().BeTrue("the buffered copy of the body is ours to release");
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task FailureWhileBufferingTheBodyDisposesTheBufferedStream(bool async)
	{
		var (config, factory) = Configure(disableDirectStreaming: true);
		var bound = new BoundConfiguration(config, null);

		Func<Task> call = async () =>
		{
			if (async)
				_ = await config.RequestInvoker.ResponseFactory.CreateAsync<TestResponse>(EndpointFor(), bound, null, null, 200, null, new BrokenConnectionStream(), "application/json", -1, null, null);
			else
				_ = config.RequestInvoker.ResponseFactory.Create<TestResponse>(EndpointFor(), bound, null, null, 200, null, new BrokenConnectionStream(), "application/json", -1, null, null);
		};

		_ = await call.Should().ThrowAsync<IOException>();

		_ = factory.Created.Should().ContainSingle().Which.IsDisposed.Should().BeTrue();
	}

	/// <summary>A response body whose connection drops while it is being read.</summary>
	private sealed class BrokenConnectionStream : Stream
	{
		public override bool CanRead => true;
		public override bool CanSeek => false;
		public override bool CanWrite => false;
		public override long Length => throw new NotSupportedException();
		public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
		public override void Flush() { }
		public override int Read(byte[] buffer, int offset, int count) => throw new IOException("connection reset");
		public override System.Threading.Tasks.Task<int> ReadAsync(byte[] buffer, int offset, int count, System.Threading.CancellationToken cancellationToken) => throw new IOException("connection reset");
#if NET
		public override System.Threading.Tasks.ValueTask<int> ReadAsync(Memory<byte> buffer, System.Threading.CancellationToken cancellationToken = default) => throw new IOException("connection reset");
		public override int Read(Span<byte> buffer) => throw new IOException("connection reset");
#endif
		public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
		public override void SetLength(long value) => throw new NotSupportedException();
		public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task InMemoryInvokerDisposesItsResponseStreamWhenDeserializationFails(bool async)
	{
		var factory = new TrackingMemoryStreamFactory();
		var invoker = new InMemoryRequestInvoker(MalformedJson);
		var config = new TransportConfiguration(new SingleNodePool(new Uri("http://localhost:9200")), invoker) with { MemoryStreamFactory = factory };
		var bound = new BoundConfiguration(config, null);

		Func<Task> call = async () =>
		{
			if (async)
				_ = await invoker.RequestAsync<TestResponse>(EndpointFor(), bound, null, default);
			else
				_ = invoker.Request<TestResponse>(EndpointFor(), bound, null);
		};

		_ = await call.Should().ThrowAsync<JsonException>();

		// Below .NET 11 the response stream comes from the factory; on .NET 11 it is a ReadOnlyMemoryStream with nothing to release.
		_ = factory.Created.All(s => s.IsDisposed).Should().BeTrue();
	}

#if !NET11_0_OR_GREATER
	// Below .NET 11 the invoker's response stream comes from the factory, so a factory that pools sees it.
	[Fact]
	public void InMemoryInvokerReleasesItsResponseStreamOnSuccess()
	{
		var factory = new TrackingMemoryStreamFactory();
		var invoker = new InMemoryRequestInvoker(Encoding.UTF8.GetBytes("{}"));
		var config = new TransportConfiguration(new SingleNodePool(new Uri("http://localhost:9200")), invoker) with { MemoryStreamFactory = factory };

		_ = invoker.Request<TestResponse>(EndpointFor(), new BoundConfiguration(config, null), null);

		_ = factory.Created.Should().ContainSingle().Which.IsDisposed.Should().BeTrue();
	}

	[Fact]
	public void InMemoryInvokerKeepsTheStreamOpenForAStreamResponseUntilItIsDisposed()
	{
		var factory = new TrackingMemoryStreamFactory();
		var invoker = new InMemoryRequestInvoker(Encoding.UTF8.GetBytes("{}"));
		var config = new TransportConfiguration(new SingleNodePool(new Uri("http://localhost:9200")), invoker) with { MemoryStreamFactory = factory };

		var response = invoker.Request<StreamResponse>(EndpointFor(), new BoundConfiguration(config, null), null);

		var created = factory.Created.Should().ContainSingle().Subject;
		_ = created.IsDisposed.Should().BeFalse("the caller reads the body from it");
		response.Dispose();
		_ = created.IsDisposed.Should().BeTrue();
	}
#endif
}
