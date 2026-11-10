// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.IO;
using Xunit;

namespace Elastic.Transport.Tests;

/// <summary>
/// The pooled factory keeps a bounded amount of idle memory. Without a bound, a burst of large bodies leaves the pool holding
/// its high-water mark for good, which is exactly what a memory dump of a healthy application shows and people read as a leak.
/// </summary>
public class RecyclableMemoryStreamFactoryTests
{
	private const int Mb = 1024 * 1024;

	/// <summary>A burst: many streams alive at once, each holding about 200 KB, then all released.</summary>
	private static void Burst(Func<MemoryStream> create, int streams = 120)
	{
		var open = Enumerable.Range(0, streams).Select(_ =>
		{
			var stream = create();
			stream.Write(new byte[200 * 1024], 0, 200 * 1024);
			return stream;
		}).ToList();

		foreach (var stream in open)
			stream.Dispose();
	}

	[Fact]
	public void AnUnboundedPoolKeepsItsWholeHighWaterMark()
	{
		// The control: the package's own default (no limit). If this did not hold on to the burst, the next test would prove nothing.
		var manager = new RecyclableMemoryStreamManager(new RecyclableMemoryStreamManager.Options { BlockSize = 16 * 1024, AggressiveBufferReturn = true });

		Burst(() => manager.GetStream());

		_ = (manager.SmallPoolFreeSize + manager.LargePoolFreeSize).Should().BeGreaterThan(10 * Mb, "120 streams of 200 KB were returned and all kept");
	}

	[Fact]
	public void ABoundedPoolNeverKeepsMoreIdleMemoryThanItsLimit()
	{
		var factory = new RecyclableMemoryStreamFactory(blockSize: 16 * 1024, maximumFreeBytes: 2 * Mb);

		Burst(() => factory.Create());

		_ = factory.FreeBytes.Should().BeLessThanOrEqualTo(2 * Mb, "the excess was left to the garbage collector");
		_ = factory.FreeBytes.Should().BeGreaterThan(0, "but it still pools up to the limit, or it would be no use");
	}

	[Fact]
	public void TheLimitHoldsAcrossRepeatedBursts()
	{
		var factory = new RecyclableMemoryStreamFactory(blockSize: 16 * 1024, maximumFreeBytes: 2 * Mb);

		for (var i = 0; i < 5; i++)
		{
			Burst(() => factory.Create(), streams: 60 + i * 20);
			_ = factory.FreeBytes.Should().BeLessThanOrEqualTo(2 * Mb, $"after burst {i}");
		}
	}

	[Fact]
	public void ADefaultFactoryIsBoundedToTheDocumentedAmount()
	{
		var factory = new RecyclableMemoryStreamFactory();

		// 50 MB is a lot to allocate in a test, so check the arithmetic through a small burst and the constant it is built from.
		Burst(() => factory.Create(), streams: 20);

		_ = factory.FreeBytes.Should().BeLessThanOrEqualTo(RecyclableMemoryStreamFactory.DefaultMaximumFreeBytes);
		_ = RecyclableMemoryStreamFactory.DefaultMaximumFreeBytes.Should().Be(50L * Mb);
	}

	[Fact]
	public void ABodyWhoseSizeDefeatsTheLimitStillWorks()
	{
		// Larger than the whole 1 MB budget: it can never be pooled, but it must be written and read like any other.
		var factory = new RecyclableMemoryStreamFactory(blockSize: 16 * 1024, maximumFreeBytes: 1 * Mb);
		var body = new byte[3 * Mb];
		new Random(11).NextBytes(body);

		using var stream = factory.Create();
		stream.Write(body, 0, body.Length);
		stream.Position = 0;
		using var copy = new MemoryStream();
		stream.CopyTo(copy);

		_ = copy.ToArray().Should().Equal(body);
	}

	[Fact]
	public void ALimitMustBePositive()
	{
		_ = ((Action)(() => _ = new RecyclableMemoryStreamFactory(16 * 1024, 0))).Should().Throw<ArgumentOutOfRangeException>();
		_ = ((Action)(() => _ = new RecyclableMemoryStreamFactory(16 * 1024, -1))).Should().Throw<ArgumentOutOfRangeException>();
	}
}
