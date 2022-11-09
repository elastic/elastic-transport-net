// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

namespace Elastic.Transport.Extensions
{
	/// <summary>
	/// A set of handy extension methods for <see cref="Serializer"/>
	/// </summary>
	public static class TransportSerializerExtensions
	{
		/// <summary>
		/// Extension method that serializes an instance of <typeparamref name="T"/> to a byte array.
		/// </summary>
		public static byte[] SerializeToBytes<T>(
			this Serializer serializer,
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
		/// <param name="serializer"><inheritdoc cref="Serializer" path="/summary"/></param>
		/// <param name="formatting"><inheritdoc cref="SerializationFormatting" path="/summary"/></param>
		public static byte[] SerializeToBytes<T>(
			this Serializer serializer,
			T data,
			MemoryStreamFactory memoryStreamFactory,
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
			this Serializer serializer,
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
		/// <param name="serializer"><inheritdoc cref="Serializer" path="/summary"/></param>
		/// <param name="formatting"><inheritdoc cref="SerializationFormatting" path="/summary"/></param>
		public static string SerializeToString<T>(
			this Serializer serializer,
			T data,
			MemoryStreamFactory memoryStreamFactory,
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
