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
	public void TheDefaultFactoryIsPooled()
	{
		using var settings = new TransportConfiguration(Node);

		_ = settings.MemoryStreamFactory.Should().BeOfType<RecyclableMemoryStreamFactory>();
		_ = TransportConfiguration.DefaultMemoryStreamFactory.Should().BeSameAs(settings.MemoryStreamFactory);
	}

	[Fact]
	public void APlainMemoryStreamIsOneAssignmentAway()
	{
		using var settings = new TransportConfiguration(Node) { MemoryStreamFactory = DefaultMemoryStreamFactory.Default };

		using var stream = settings.MemoryStreamFactory.Create();

		_ = settings.MemoryStreamFactory.Should().BeOfType<DefaultMemoryStreamFactory>();
		_ = stream.GetType().Should().Be<MemoryStream>();
	}

	[Fact]
	public void ABodyLargerThanOneBlockIsHeldWithoutLosingAnyOfIt()
	{
		// 16 KB blocks: this spans many of them, and must come back byte for byte.
		var body = new byte[100_000];
		new Random(7).NextBytes(body);

		using var stream = TransportConfiguration.DefaultMemoryStreamFactory.Create();
		stream.Write(body, 0, body.Length);
		stream.Position = 0;
		using var copy = new MemoryStream();
		stream.CopyTo(copy);

		_ = copy.ToArray().Should().Equal(body);
	}
}
