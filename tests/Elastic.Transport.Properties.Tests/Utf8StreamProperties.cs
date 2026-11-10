// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.IO;
using System.Linq;
using System.Text;
using CsCheck;
using Elastic.Transport.Extensions;
using FluentAssertions;
using Xunit;

namespace Elastic.Transport.Properties.Tests;

/// <summary>
/// ReadOnlyStreams must yield exactly what UTF-8 encoding the text yields, whatever the text and however it is read.
/// On net11 these are StringStream and ReadOnlyMemoryStream, which encode as they are read: a surrogate pair or a
/// multi-byte character can straddle two reads, and a lone surrogate has to come out as the replacement character.
/// </summary>
public class Utf8StreamProperties
{
	// Any UTF-16 code unit, so lone and mismatched surrogates are included.
	private static readonly Gen<string> AnyText =
		Gen.Int[0, 0xFFFF].Select(i => (char)i).Array[0, 400].Select(chars => new string(chars));

	private static byte[] ReadWith(Stream stream, int bufferSize)
	{
		using var all = new MemoryStream();
		var buffer = new byte[bufferSize];
		int read;
		while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
			all.Write(buffer, 0, read);
		return all.ToArray();
	}

	[Fact]
	public void AStringReadInAnySizedPiecesIsItsUtf8Bytes() =>
		Gen.Select(AnyText, Gen.Int[1, 64]).Sample((text, bufferSize) =>
		{
			using var stream = ReadOnlyStreams.OverUtf8(DefaultMemoryStreamFactory.Default, text);

			_ = ReadWith(stream, bufferSize).Should().Equal(Encoding.UTF8.GetBytes(text), $"read in pieces of {bufferSize}");
		}, iter: 1000);

	[Fact]
	public void ASpanOfCharactersIsItsUtf8Bytes() =>
		Gen.Select(AnyText, Gen.Int[1, 64]).Sample((text, bufferSize) =>
		{
			using var stream = ReadOnlyStreams.OverUtf8(DefaultMemoryStreamFactory.Default, text.AsSpan());

			_ = ReadWith(stream, bufferSize).Should().Equal(Encoding.UTF8.GetBytes(text));
		}, iter: 500);

	[Fact]
	public void BytesReadInAnySizedPiecesComeBackWhole() =>
		Gen.Select(Gen.Byte.Array[0, 2000], Gen.Int[1, 97]).Sample((bytes, bufferSize) =>
		{
			using var stream = ReadOnlyStreams.Over(DefaultMemoryStreamFactory.Default, bytes);

			_ = ReadWith(stream, bufferSize).Should().Equal(bytes);
		}, iter: 500);
}
