// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.IO;

namespace Elastic.Transport;

/// <summary>
/// A factory for creating memory streams
/// </summary>
public abstract class MemoryStreamFactory
{
	/// <summary>
	/// Constructs a new instance of <see cref="MemoryStreamFactory"/>.
	/// </summary>
	protected MemoryStreamFactory() { }

	/// <summary>
	/// Creates a memory stream
	/// </summary>
	public abstract MemoryStream Create();

	/// <summary>
	/// Creates a memory stream with the bytes written to the stream
	/// </summary>
	public abstract MemoryStream Create(byte[] bytes);

	/// <summary>
	/// Creates a memory stream with the bytes written to the stream
	/// </summary>
	public abstract MemoryStream Create(byte[] bytes, int index, int count);
}
