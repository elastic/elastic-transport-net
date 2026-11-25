// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

namespace Elastic.Transport;

/// <summary>
/// A type of response that makes it easier to work with responses in an untyped fashion.
///
/// <para>It exposes the body as <see cref="DynamicDictionary"/> which is `dynamic` through <see cref="System.Dynamic.DynamicObject"/></para>
/// <para></para>
/// <para>Since `dynamic` can be scary in .NET this response also exposes a safe traversal mechanism under
/// <see cref="DynamicResponseBase.Get{T}"/> which support an xpath'esque syntax to fish for values in the returned json.
/// </para>
/// </summary>
public sealed class DynamicResponse : DynamicResponseBase
{
	/// <inheritdoc cref="DynamicResponse"/>
	public DynamicResponse() { }

	/// <inheritdoc cref="DynamicResponse"/>
	public DynamicResponse(DynamicDictionary dictionary) : base(dictionary) { }
}
