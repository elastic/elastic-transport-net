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

namespace Elastic.Transport.Extensions
{
	/// <summary>
	/// A set of handy extension methods for <see cref="ITransportSerializer"/>
	/// </summary>
	public static class TransportSerializerExtensions
	{
		/// <summary>
		/// Extension method that serializes an instance of <typeparamref name="T"/> to a byte array.
		/// </summary>
		public static byte[] SerializeToBytes<T>(
			this ITransportSerializer serializer,
			T data,
			SerializationFormatting formatting = SerializationFormatting.None) =>
			SerializeToBytes(serializer, data, TransportConfiguration.DefaultMemoryStreamFactory, formatting);

		/// <summary>
		/// Extension method that serializes an instance of <typeparamref name="T"/> to a byte array.
		/// </summary>
		/// <param name="data"></param>
		/// <param name="memoryStreamFactory">
		/// A factory yielding MemoryStream instances, defaults to <see cref="RecyclableMemoryStreamFactory"/>
		/// that yields memory streams backed by pooled byte arrays.
		/// </param>
		/// <param name="serializer"><inheritdoc cref="ITransportSerializer" path="/summary"/></param>
		/// <param name="formatting"><inheritdoc cref="SerializationFormatting" path="/summary"/></param>
		public static byte[] SerializeToBytes<T>(
			this ITransportSerializer serializer,
			T data,
			IMemoryStreamFactory memoryStreamFactory,
			SerializationFormatting formatting = SerializationFormatting.None
		)
		{
			memoryStreamFactory ??= TransportConfiguration.DefaultMemoryStreamFactory;
			using (var ms = memoryStreamFactory.Create())
			{
				serializer.Serialize(data, ms, formatting);
				return ms.ToArray();
			}
		}

		/// <summary>
		/// Extension method that serializes an instance of <typeparamref name="T"/> to a string.
		/// </summary>
		public static string SerializeToString<T>(
			this ITransportSerializer serializer,
			T data,
			SerializationFormatting formatting = SerializationFormatting.None) =>
			SerializeToString(serializer, data, TransportConfiguration.DefaultMemoryStreamFactory, formatting);

		/// <summary>
		/// Extension method that serializes an instance of <typeparamref name="T"/> to a string.
		/// </summary>
		/// <param name="data"></param>
		/// <param name="memoryStreamFactory">
		/// A factory yielding MemoryStream instances, defaults to <see cref="RecyclableMemoryStreamFactory"/>
		/// that yields memory streams backed by pooled byte arrays.
		/// </param>
		/// <param name="serializer"><inheritdoc cref="ITransportSerializer" path="/summary"/></param>
		/// <param name="formatting"><inheritdoc cref="SerializationFormatting" path="/summary"/></param>
		public static string SerializeToString<T>(
			this ITransportSerializer serializer,
			T data,
			IMemoryStreamFactory memoryStreamFactory,
			SerializationFormatting formatting = SerializationFormatting.None
		)
		{
			memoryStreamFactory ??= TransportConfiguration.DefaultMemoryStreamFactory;
			using (var ms = memoryStreamFactory.Create())
			{
				serializer.Serialize(data, ms, formatting);
				return ms.Utf8String();
			}
		}

	}
}
