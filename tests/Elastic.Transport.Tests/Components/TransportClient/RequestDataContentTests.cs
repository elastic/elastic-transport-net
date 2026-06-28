// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

#if !NETFRAMEWORK
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

using CompleteTaskOnCloseStream = Elastic.Transport.BoundConfigurationContent.CompleteTaskOnCloseStream;

namespace Elastic.Transport.Tests.Components.TransportClient;

/// <summary>
/// Regression coverage for the request serialization stream wrappers in <c>RequestDataContent</c>.
/// <para>
/// <see cref="CompleteTaskOnCloseStream"/> owns the linked <see cref="CancellationTokenSource"/> created per
/// async request and must dispose it when the stream is disposed. A linked source keeps a callback registered on
/// each parent token, so until it is disposed it stays rooted from those (potentially application-lifetime) parents.
/// Under sustained high request volume an undisposed source per request accumulates into a multi-gigabyte leak.
/// </para>
/// <para>
/// The leak was caused by overriding <see cref="Stream.Close"/> without chaining to the base implementation, which
/// short-circuits <c>Stream.Dispose() -> Close() -> Dispose(bool)</c> and silently skips the source disposal. These
/// tests exercise every disposal entry point the serialization paths actually use.
/// </para>
/// </summary>
public class RequestDataContentTests
{
	[Fact]
	public void DisposeDisposesOwnedCancellationTokenSource()
	{
		var stream = CreateWrappedStream(out var source, out _, out _);

		stream.Dispose();

		SourceShouldBeDisposed(source);
	}

	[Fact]
	public async Task DisposeAsyncDisposesOwnedCancellationTokenSource()
	{
		var stream = CreateWrappedStream(out var source, out _, out _);

		await stream.DisposeAsync();

		SourceShouldBeDisposed(source);
	}

	[Fact]
	public void CloseDisposesOwnedCancellationTokenSource()
	{
		var stream = CreateWrappedStream(out var source, out _, out _);

		stream.Close();

		SourceShouldBeDisposed(source);
	}

	[Fact]
	public void DisposeCompletesSerializeToStreamTask()
	{
		var stream = CreateWrappedStream(out _, out var serializeToStreamTask, out _);

		stream.Dispose();

		serializeToStreamTask.Task.IsCompleted.Should().BeTrue();
	}

	[Fact]
	public void DisposeDoesNotDisposeInnerPipelineStream()
	{
		// HttpContent must not dispose the stream it serializes to; that stream is owned by the HttpClient pipeline.
		var stream = CreateWrappedStream(out _, out _, out var inner);

		stream.Dispose();

		inner.CanRead.Should().BeTrue("the wrapper must not dispose the pipeline-owned stream");
	}

	private static CompleteTaskOnCloseStream CreateWrappedStream(
		out CancellationTokenSource source, out TaskCompletionSource<bool> serializeToStreamTask, out MemoryStream inner)
	{
		// Mirror the production path: a token source linked to a (long-lived) request token, which is exactly the
		// object that leaks when it is not disposed.
		using var parent = new CancellationTokenSource();
		source = CancellationTokenSource.CreateLinkedTokenSource(parent.Token, default);
		serializeToStreamTask = new TaskCompletionSource<bool>();
		inner = new MemoryStream();
		return new CompleteTaskOnCloseStream(inner, serializeToStreamTask, source);
	}

	private static void SourceShouldBeDisposed(CancellationTokenSource source)
	{
		// A disposed CancellationTokenSource throws when its token is accessed; this is the observable proof the
		// source was disposed and its parent-token callbacks unregistered.
		var act = () => _ = source.Token;
		act.Should().Throw<ObjectDisposedException>();
	}
}
#endif
