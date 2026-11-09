// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

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

	/// <summary>A factory over the package's defaults (128 KB blocks), returning buffers to the pool aggressively.</summary>
	public RecyclableMemoryStreamFactory() : this(blockSize: 128 * 1024) { }

	/// <summary>
	/// A factory whose pool hands out blocks of <paramref name="blockSize"/> bytes. Every stream in use holds at least one
	/// block, so smaller blocks keep less memory pooled when many requests are in flight.
	/// </summary>
	public RecyclableMemoryStreamFactory(int blockSize) =>
		_manager = new RecyclableMemoryStreamManager(new RecyclableMemoryStreamManager.Options { BlockSize = blockSize, AggressiveBufferReturn = true });

	/// <summary> Provide a static instance of this stateless class, so it can be reused</summary>
	public static RecyclableMemoryStreamFactory Default { get; } = new RecyclableMemoryStreamFactory();

	/// <inheritdoc />
	public override MemoryStream Create() => _manager.GetStream(TagSource);

	/// <inheritdoc />
	public override MemoryStream Create(byte[] bytes) => _manager.GetStream(bytes);

	/// <inheritdoc />
	public override MemoryStream Create(byte[] bytes, int index, int count) => _manager.GetStream(TagSource, bytes, index, count);
}
