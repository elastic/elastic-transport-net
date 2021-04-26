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
using Elastic.Transport.Extensions;

namespace Elastic.Transport
{
	public abstract partial class PostData
	{
		/// <summary>
		/// Create a <see cref="PostData"/> instance that will write <paramref name="serializedString"/> to the output <see cref="Stream"/>
		/// </summary>
		// ReSharper disable once MemberCanBePrivate.Global
		public static PostData String(string serializedString) => new PostDataString(serializedString);

		/// <summary>
		/// string implicitly converts to <see cref="PostData"/> so you do not have to use the static <see cref="String"/>
		/// factory method
		/// </summary>
		public static implicit operator PostData(string literalString) => String(literalString);

		private class PostDataString : PostData
		{
			private readonly string _literalString;

			protected internal PostDataString(string item)
			{
				_literalString = item;
				Type = PostType.LiteralString;
			}

			public override void Write(Stream writableStream, ITransportConfiguration settings)
			{
				if (string.IsNullOrEmpty(_literalString)) return;

				var stream = InitWrite(writableStream, settings, out var buffer, out var disableDirectStreaming);

				var stringBytes = WrittenBytes ?? _literalString.Utf8Bytes();
				WrittenBytes ??= stringBytes;
				if (!disableDirectStreaming)
					stream.Write(stringBytes, 0, stringBytes.Length);
				else
					buffer = settings.MemoryStreamFactory.Create(stringBytes);

				FinishStream(writableStream, buffer, settings);
			}

			public override async Task WriteAsync(Stream writableStream, ITransportConfiguration settings,
				CancellationToken cancellationToken)
			{
				if (string.IsNullOrEmpty(_literalString)) return;

				var stream = InitWrite(writableStream, settings, out var buffer, out var disableDirectStreaming);

				var stringBytes = WrittenBytes ?? _literalString.Utf8Bytes();
				WrittenBytes ??= stringBytes;
				if (!disableDirectStreaming)
					await stream.WriteAsync(stringBytes, 0, stringBytes.Length, cancellationToken)
						.ConfigureAwait(false);
				else
					buffer = settings.MemoryStreamFactory.Create(stringBytes);

				await FinishStreamAsync(writableStream, buffer, settings, cancellationToken).ConfigureAwait(false);
			}
		}
	}
}
