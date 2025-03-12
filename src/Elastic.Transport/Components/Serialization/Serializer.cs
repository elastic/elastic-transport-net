// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Elastic.Transport;

/// <summary>
/// When the <see cref="ITransport{TConfiguration}"/> needs to (de)serialize anything it will call into the
/// <see cref="ITransportConfiguration.RequestResponseSerializer"/> implementation of this base class.
///
/// <para>e.g: Whenever the <see cref="ITransport{TConfiguration}"/> receives <see cref="PostData.Serializable{T}"/>
/// to serialize that data.</para>
/// </summary>
public abstract class Serializer
{
	// TODO: Overloads taking a Memory<T>/Span<T>??

	/// <summary> Deserialize <paramref name="stream"/> to an instance of <paramref name="type"/> </summary>
	public abstract object? Deserialize(Type type, Stream stream);

	/// <summary> Deserialize <paramref name="stream"/> to an instance of <typeparamref name="T" /></summary>
	public abstract T Deserialize<T>(Stream stream);

	/// <inheritdoc cref="Deserialize"/>
	public abstract ValueTask<object?> DeserializeAsync(Type type, Stream stream, CancellationToken cancellationToken = default);

	/// <inheritdoc cref="Deserialize"/>
	public abstract ValueTask<T> DeserializeAsync<T>(Stream stream, CancellationToken cancellationToken = default);

	/// <inheritdoc cref="Serialize{T}"/>
	public abstract void Serialize(
		object? data,
		Type type,
		Stream stream,
		SerializationFormatting formatting = SerializationFormatting.None,
		CancellationToken cancellationToken = default
	);

	/// <summary>
	/// Serialize an instance of <typeparamref name="T"/> to <paramref name="stream"/> using <paramref name="formatting"/>.
	/// </summary>
	/// <param name="data">The instance of <typeparamref name="T"/> that we want to serialize.</param>
	/// <param name="stream">The stream to serialize to.</param>
	/// <param name="formatting">
	/// Formatting hint. Note that not all implementations of <see cref="Serializer"/> are able to
	/// satisfy this hint, including the default serializer that is shipped with 8.0.
	/// </param>
	public abstract void Serialize<T>(
		T data,
		Stream stream,
		SerializationFormatting formatting = SerializationFormatting.None
	);

	/// <inheritdoc cref="Serialize{T}"/>
	public abstract Task SerializeAsync(
		object? data,
		Type type,
		Stream stream,
		SerializationFormatting formatting = SerializationFormatting.None,
		CancellationToken cancellationToken = default
	);

	/// <inheritdoc cref="Serialize{T}"/>
	public abstract Task SerializeAsync<T>(
		T data,
		Stream stream,
		SerializationFormatting formatting = SerializationFormatting.None,
		CancellationToken cancellationToken = default
	);
}
