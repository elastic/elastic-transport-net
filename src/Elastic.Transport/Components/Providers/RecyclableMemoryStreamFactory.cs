// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.IO;

namespace Elastic.Transport
{
	/// <summary>
	/// A factory for creating memory streams using a recyclable pool of <see cref="MemoryStream" /> instances
	/// </summary>
	public sealed class RecyclableMemoryStreamFactory : MemoryStreamFactory
	{
		private const string TagSource = "Elastic.Transport";
		private readonly RecyclableMemoryStreamManager _manager;

		/// <summary> Provide a static instance of this stateless class, so it can be reused</summary>
		public static RecyclableMemoryStreamFactory Default { get; } = new RecyclableMemoryStreamFactory();

		/// <inheritdoc cref="RecyclableMemoryStream"/>
		public RecyclableMemoryStreamFactory() => _manager = CreateManager(experimental: false);

		private static RecyclableMemoryStreamManager CreateManager(bool experimental)
		{
			if (!experimental) return new RecyclableMemoryStreamManager() { AggressiveBufferReturn = true };

			const int blockSize = 1024;
			const int largeBufferMultiple = 1024 * 1024;
			const int maxBufferSize = 16 * largeBufferMultiple;
			return new RecyclableMemoryStreamManager(blockSize, largeBufferMultiple, maxBufferSize)
			{
				AggressiveBufferReturn = true, MaximumFreeLargePoolBytes = maxBufferSize * 4, MaximumFreeSmallPoolBytes = 100 * blockSize
			};
		}

		/// <inheritdoc />
		public override MemoryStream Create() => _manager.GetStream(Guid.Empty, TagSource);

		/// <inheritdoc />
		public override MemoryStream Create(byte[] bytes) => _manager.GetStream(bytes);

		/// <inheritdoc />
		public override MemoryStream Create(byte[] bytes, int index, int count) => _manager.GetStream(Guid.Empty, TagSource, bytes, index, count);
	}
}
