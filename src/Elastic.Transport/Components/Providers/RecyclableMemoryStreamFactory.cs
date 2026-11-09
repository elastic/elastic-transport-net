// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.IO;
using Microsoft.IO;

namespace Elastic.Transport;

/// <summary>
/// A factory for creating memory streams using a recyclable pool of <see cref="MemoryStream" /> instances, backed by
/// the <c>Microsoft.IO.RecyclableMemoryStream</c> package.
/// </summary>
public sealed class RecyclableMemoryStreamFactory : MemoryStreamFactory
{
	private const string TagSource = "Elastic.Transport";
	private readonly RecyclableMemoryStreamManager _manager;

	/// <summary>
	/// The most idle memory a pool keeps by default, in bytes: 50 MB, split evenly between small blocks and large buffers.
	/// Memory in use by live requests is not part of this; it is only what is held for reuse.
	/// </summary>
	public const long DefaultMaximumFreeBytes = 50L * 1024 * 1024;

	/// <summary>A factory over 128 KB blocks that keeps at most <see cref="DefaultMaximumFreeBytes"/> idle.</summary>
	public RecyclableMemoryStreamFactory() : this(blockSize: 128 * 1024) { }

	/// <summary>
	/// A factory whose pool hands out blocks of <paramref name="blockSize"/> bytes. Every stream in use holds at least one
	/// block, so smaller blocks keep less memory pooled when many requests are in flight. At most
	/// <see cref="DefaultMaximumFreeBytes"/> is kept idle.
	/// </summary>
	public RecyclableMemoryStreamFactory(int blockSize) : this(blockSize, DefaultMaximumFreeBytes) { }

	/// <summary>
	/// A factory whose pool keeps at most <paramref name="maximumFreeBytes"/> idle for reuse. A buffer returned beyond that is
	/// left to the garbage collector, so what a memory dump shows as pooled has a ceiling. A buffer too large to ever fit is
	/// never pooled. Memory held by streams that are in use is not counted.
	/// </summary>
	/// <param name="blockSize">The size of the blocks the pool hands out.</param>
	/// <param name="maximumFreeBytes">The most idle memory to keep, split evenly between blocks and large buffers. Must be positive.</param>
	public RecyclableMemoryStreamFactory(int blockSize, long maximumFreeBytes)
	{
		if (maximumFreeBytes <= 0)
			throw new ArgumentOutOfRangeException(nameof(maximumFreeBytes), maximumFreeBytes, "The pool must be given a positive limit.");

		const int largeBufferMultiple = 1024 * 1024;
		var half = maximumFreeBytes / 2;

		// Buffers bigger than half the budget could never be kept, so do not pool them at all. The pool needs this to be a
		// whole number of large-buffer steps and at least one block, so round up to that; a buffer that big then simply does
		// not fit under a small limit and is left to the garbage collector when it is returned.
		var largest = (int)Math.Min(16L * largeBufferMultiple, half / largeBufferMultiple * largeBufferMultiple);
		largest = Math.Max(largest, blockSize);
		largest = (largest + largeBufferMultiple - 1) / largeBufferMultiple * largeBufferMultiple;

		_manager = new RecyclableMemoryStreamManager(new RecyclableMemoryStreamManager.Options
		{
			BlockSize = blockSize,
			LargeBufferMultiple = largeBufferMultiple,
			MaximumBufferSize = largest,
			MaximumSmallPoolFreeBytes = half,
			MaximumLargePoolFreeBytes = half,
			AggressiveBufferReturn = true,
		});
	}

	/// <summary>The memory the pool is holding idle right now. For tests and diagnostics.</summary>
	internal long FreeBytes => _manager.SmallPoolFreeSize + _manager.LargePoolFreeSize;

	/// <summary> Provide a static instance of this stateless class, so it can be reused</summary>
	public static RecyclableMemoryStreamFactory Default { get; } = new RecyclableMemoryStreamFactory();

	/// <inheritdoc />
	public override MemoryStream Create() => _manager.GetStream(TagSource);

	/// <inheritdoc />
	public override MemoryStream Create(byte[] bytes) => _manager.GetStream(bytes);

	/// <inheritdoc />
	public override MemoryStream Create(byte[] bytes, int index, int count) => _manager.GetStream(TagSource, bytes, index, count);
}
