/*
 * Licensed to Elasticsearch B.V. under one or more contributor
 * license agreements. See the NOTICE file distributed with
 * this work for additional information regarding copyright
 * ownership. Elasticsearch B.V. licenses this file to you under
 * the Apache License, Version 2.0 (the "License"); you may
 * not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *    http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing,
 * software distributed under the License is distributed on an
 * "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
 * KIND, either express or implied.  See the License for the
 * specific language governing permissions and limitations
 * under the License.
 */

#if !NETSTANDARD2_0 && !NETFRAMEWORK
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Elastic.Transport
{
	public abstract partial class PostData
	{
		/// <summary>
		/// Create a <see cref="PostData"/> instance that will write the <paramref name="bytes"/> to the output <see cref="Stream"/>
		/// </summary>
		public static PostData ReadOnlyMemory(ReadOnlyMemory<byte> bytes) => new PostDataReadOnlyMemory(bytes);

		private class PostDataReadOnlyMemory : PostData
		{
			private readonly ReadOnlyMemory<byte> _memoryOfBytes;

			protected internal PostDataReadOnlyMemory(ReadOnlyMemory<byte> item)
			{
				_memoryOfBytes = item;
				Type = PostType.ReadOnlyMemory;
			}

			public override void Write(Stream writableStream, ITransportConfiguration settings)
			{
				if (_memoryOfBytes.IsEmpty) return;

				var stream = InitWrite(writableStream, settings, out var buffer, out var disableDirectStreaming);

				if (!disableDirectStreaming)
					stream.Write(_memoryOfBytes.Span);
				else
				{
					WrittenBytes ??= _memoryOfBytes.Span.ToArray();
					buffer = settings.MemoryStreamFactory.Create(WrittenBytes);
				}
				FinishStream(writableStream, buffer, settings);
			}

			public override async Task WriteAsync(Stream writableStream, ITransportConfiguration settings,
				CancellationToken cancellationToken)
			{
				if (_memoryOfBytes.IsEmpty) return;

				var stream = InitWrite(writableStream, settings, out var buffer, out var disableDirectStreaming);

				if (!disableDirectStreaming)
					stream.Write(_memoryOfBytes.Span);
				else
				{
					WrittenBytes ??= _memoryOfBytes.Span.ToArray();
					buffer = settings.MemoryStreamFactory.Create(WrittenBytes);
				}

				await FinishStreamAsync(writableStream, buffer, settings, cancellationToken).ConfigureAwait(false);
			}
		}
	}
}
#endif
