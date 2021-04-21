// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Elastic.Transport
{
	/// <summary>
	/// When the <see cref="ITransport{TConnectionSettings}"/> needs to (de)serialize anything it will call into the
	/// <see cref="ITransportConfiguration.RequestResponseSerializer"/>  implementation of this interface.
	///
	/// <para>e.g: Whenever the <see cref="ITransport{TConnectionSettings}"/> receives <see cref="PostData.Serializable{T}"/>
	/// to serialize that data.</para>
	/// <para>Or when <see cref="ResponseBuilder.ToResponse{TResponse}"/> determines the response needs to be deserialized.</para>
	/// </summary>
	public interface ITransportSerializer
	{
		/// <summary> Deserialize <paramref name="stream"/> to an instance of <paramref name="type"/> </summary>
		object Deserialize(Type type, Stream stream);

		/// <summary> Deserialize <paramref name="stream"/> to an instance of <typeparamref name="T" /></summary>
		T Deserialize<T>(Stream stream);

		/// <inheritdoc cref="Deserialize"/>
		Task<object> DeserializeAsync(Type type, Stream stream, CancellationToken cancellationToken = default);

		/// <inheritdoc cref="Deserialize"/>
		Task<T> DeserializeAsync<T>(Stream stream, CancellationToken cancellationToken = default);

		/// <summary>
		/// Serialize an instance of <typeparamref name="T"/> to <paramref name="stream"/> using <paramref name="formatting"/>.
		/// </summary>
		/// <param name="data">The instance of <typeparamref name="T"/> that we want to serialize</param>
		/// <param name="stream">The stream to serialize to</param>
		/// <param name="formatting">
		/// Formatting hint, note no all implementations of <see cref="ITransportSerializer"/> are able to
		/// satisfy this hint, including the default serializer that is shipped with 7.0.
		/// </param>
		void Serialize<T>(T data, Stream stream, SerializationFormatting formatting = SerializationFormatting.None);

		/// <inheritdoc cref="Serialize{T}"/>
		Task SerializeAsync<T>(
			T data,
			Stream stream,
			SerializationFormatting formatting = SerializationFormatting.None,
			CancellationToken cancellationToken = default
		);
	}
}
