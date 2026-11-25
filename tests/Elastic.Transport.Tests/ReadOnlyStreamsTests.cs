// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.IO;
using System.Text;
using Elastic.Transport.Extensions;
using FluentAssertions;
using Xunit;

namespace Elastic.Transport.Tests;

public class ReadOnlyStreamsTests
{
	private static readonly MemoryStreamFactory Factory = DefaultMemoryStreamFactory.Default;

	private static byte[] ReadAll(Stream stream)
	{
		using var copy = new MemoryStream();
		stream.CopyTo(copy);
		return copy.ToArray();
	}

	[Fact]
	public void BytesAreReadAsTheyAre()
	{
		var bytes = new byte[] { 1, 2, 3, 250, 255 };

		using var stream = ReadOnlyStreams.Over(Factory, bytes);

		_ = ReadAll(stream).Should().Equal(bytes);
	}

	[Fact]
	public void AStringIsReadAsUtf8WhateverItsCharacters()
	{
		// Multi-byte characters and a surrogate pair: a stream that encodes as it reads must not split or mangle them.
		const string text = "{\"name\":\"héllo ✓ 日本語 😀\"}";

		using var stream = ReadOnlyStreams.OverUtf8(Factory, text);

		_ = ReadAll(stream).Should().Equal(Encoding.UTF8.GetBytes(text));
	}

	[Fact]
	public void ASpanOfCharactersIsReadAsUtf8()
	{
		const string text = "{\"a\":\"é😀\"}";

		using var stream = ReadOnlyStreams.OverUtf8(Factory, text.AsSpan());

		_ = ReadAll(stream).Should().Equal(Encoding.UTF8.GetBytes(text));
	}

	[Fact]
	public void ALongStringSpanningManyReadsArrivesWhole()
	{
		var text = "{\"v\":\"" + string.Concat(System.Linq.Enumerable.Repeat("é😀x", 20_000)) + "\"}";

		using var stream = ReadOnlyStreams.OverUtf8(Factory, text);

		_ = ReadAll(stream).Should().Equal(Encoding.UTF8.GetBytes(text));
	}

#if NET11_0_OR_GREATER
	[Fact]
	public void OnNet11TheBuiltInStreamsAreUsedAndTheFactoryIsNotAsked()
	{
		var factory = new CountingFactory();

		using var bytes = ReadOnlyStreams.Over(factory, [1, 2, 3]);
		using var text = ReadOnlyStreams.OverUtf8(factory, "abc");

		_ = bytes.Should().BeOfType<ReadOnlyMemoryStream>();
		_ = text.Should().BeOfType<StringStream>();
		_ = factory.Created.Should().Be(0, "wrapping data we hold needs no pooled copy");
	}
#else
	[Fact]
	public void BeforeNet11TheFactoryIsStillUsed()
	{
		var factory = new CountingFactory();

		using var bytes = ReadOnlyStreams.Over(factory, [1, 2, 3]);
		using var text = ReadOnlyStreams.OverUtf8(factory, "abc");

		_ = factory.Created.Should().Be(2, "a custom factory keeps seeing these calls where the built-in streams do not exist");
	}
#endif

	private sealed class CountingFactory : MemoryStreamFactory
	{
		public int Created { get; private set; }

		public override MemoryStream Create() { Created++; return new MemoryStream(); }

		public override MemoryStream Create(byte[] bytes) { Created++; return new MemoryStream(bytes); }

		public override MemoryStream Create(byte[] bytes, int index, int count) { Created++; return new MemoryStream(bytes, index, count); }
	}
}
