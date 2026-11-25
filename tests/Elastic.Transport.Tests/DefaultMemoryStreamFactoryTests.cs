// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.IO;
using FluentAssertions;
using Xunit;

namespace Elastic.Transport.Tests;

public class DefaultMemoryStreamFactoryTests
{
	private static readonly Uri Node = new("http://localhost:9200");

	[Fact]
	public void TheDefaultFactoryIsAPlainMemoryStream()
	{
		// Deliberate: a pool shows up in memory dumps and reads as a leak. Pooling is opt-in.
		using var settings = new TransportConfiguration(Node);

		using var stream = settings.MemoryStreamFactory.Create();

		_ = settings.MemoryStreamFactory.Should().BeOfType<DefaultMemoryStreamFactory>();
		_ = TransportConfiguration.DefaultMemoryStreamFactory.Should().BeSameAs(settings.MemoryStreamFactory);
		_ = stream.GetType().Should().Be<MemoryStream>();
	}

	[Fact]
	public void PoolingIsOneAssignmentAway()
	{
		using var settings = new TransportConfiguration(Node) { MemoryStreamFactory = new RecyclableMemoryStreamFactory(blockSize: 16 * 1024) };

		_ = settings.MemoryStreamFactory.Should().BeOfType<RecyclableMemoryStreamFactory>();
	}

	[Fact]
	public void ABodyLargerThanOneBlockIsHeldWithoutLosingAnyOfIt()
	{
		// A pooled stream with 16 KB blocks: this spans many of them, and must come back byte for byte.
		var body = new byte[100_000];
		new Random(7).NextBytes(body);

		using var stream = new RecyclableMemoryStreamFactory(blockSize: 16 * 1024).Create();
		stream.Write(body, 0, body.Length);
		stream.Position = 0;
		using var copy = new MemoryStream();
		stream.CopyTo(copy);

		_ = copy.ToArray().Should().Equal(body);
	}
}
