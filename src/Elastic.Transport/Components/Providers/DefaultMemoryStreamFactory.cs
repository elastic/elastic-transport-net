// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.IO;

namespace Elastic.Transport;

/// <summary>
/// A factory for creating memory streams using instances of <see cref="MemoryStream" />
/// </summary>
public sealed class DefaultMemoryStreamFactory : MemoryStreamFactory
{
	/// <summary> Provide a static instance of this stateless class, so it can be reused</summary>
	public static DefaultMemoryStreamFactory Default { get; } = new DefaultMemoryStreamFactory();

	/// <inheritdoc />
	public override MemoryStream Create() => new();

	/// <inheritdoc />
	public override MemoryStream Create(byte[] bytes) => new(bytes);

	/// <inheritdoc />
	public override MemoryStream Create(byte[] bytes, int index, int count) => new(bytes, index, count);
}
