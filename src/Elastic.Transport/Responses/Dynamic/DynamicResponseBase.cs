// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Diagnostics.CodeAnalysis;
using System.Dynamic;

namespace Elastic.Transport;

/// <summary>
/// Base class for responses that expose the body as <see cref="DynamicDictionary"/>.
///
/// <para>It exposes the body as <see cref="DynamicDictionary"/> which is <c>dynamic</c> through <see cref="DynamicObject"/>.</para>
/// <para>Also provides safe traversal via <see cref="Get{T}"/> using xpath-style dot-notation paths.</para>
/// </summary>
public abstract class DynamicResponseBase : TransportResponse<DynamicDictionary>
{
	/// <inheritdoc cref="DynamicResponseBase"/>
	protected DynamicResponseBase()
	{
		Body = DynamicDictionary.Empty;
		Dictionary = DynamicDictionary.Empty;
	}

	/// <inheritdoc cref="DynamicResponseBase"/>
	protected DynamicResponseBase(DynamicDictionary dictionary)
	{
		Body = dictionary;
		Dictionary = dictionary;
	}

	private DynamicDictionary Dictionary { get; }

	/// <summary>
	/// Traverses data using path notation.
	/// <para><c>e.g some.deep.nested.json.path</c></para>
	/// <para> A special lookup is available for ANY key using <c>_arbitrary_key_</c> <c>e.g some.deep._arbitrary_key_.json.path</c> which will traverse into the first key</para>
	/// </summary>
	/// <param name="path">path into the stored object, keys are separated with a dot and the last key is returned as T</param>
	/// <typeparam name="T"></typeparam>
	/// <returns>T or default</returns>
	public T Get<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(string path) => Dictionary.Get<T>(path);
}
