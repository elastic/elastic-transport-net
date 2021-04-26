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

using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Elastic.Transport
{
	public abstract partial class PostData
	{
		/// <summary>
		/// Create a <see cref="PostData"/> instance that will write <paramref name="bytes"/> to the output <see cref="Stream"/>
		/// </summary>
		// ReSharper disable once MemberCanBePrivate.Global
		public static PostData Bytes(byte[] bytes) => new PostDataByteArray(bytes);

		private class PostDataByteArray : PostData
		{
			protected internal PostDataByteArray(byte[] item)
			{
				WrittenBytes = item;
				Type = PostType.ByteArray;
			}

			public override void Write(Stream writableStream, ITransportConfiguration settings)
			{
				if (WrittenBytes == null) return;

				var stream = InitWrite(writableStream, settings, out var buffer, out var disableDirectStreaming);

				if (!disableDirectStreaming)
					stream.Write(WrittenBytes, 0, WrittenBytes.Length);
				else
					buffer = settings.MemoryStreamFactory.Create(WrittenBytes);

				FinishStream(writableStream, buffer, settings);
			}

			public override async Task WriteAsync(Stream writableStream, ITransportConfiguration settings,
				CancellationToken cancellationToken)
			{
				if (WrittenBytes == null) return;

				var stream = InitWrite(writableStream, settings, out var buffer, out var disableDirectStreaming);

				if (!disableDirectStreaming)
					await stream.WriteAsync(WrittenBytes, 0, WrittenBytes.Length, cancellationToken)
						.ConfigureAwait(false);
				else
					buffer = settings.MemoryStreamFactory.Create(WrittenBytes);

				await FinishStreamAsync(writableStream, buffer, settings, cancellationToken).ConfigureAwait(false);
			}
		}
	}
}
