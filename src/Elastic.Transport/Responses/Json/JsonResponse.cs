// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Nodes;

namespace Elastic.Transport;

/// <summary>
/// A response type backed by <see cref="JsonNode"/> from System.Text.Json.
///
/// <para>Provides direct DOM access via <see cref="TransportResponse{T}.Body"/> (e.g. <c>response.Body["hits"]["hits"][0]</c>)</para>
/// <para>Also exposes a safe path traversal mechanism via <see cref="Get{T}"/> using dot-notation paths.</para>
/// </summary>
public sealed class JsonResponse : TransportResponse<JsonNode>
{
	/// <inheritdoc cref="JsonResponse"/>
	public JsonResponse() { }

	/// <inheritdoc cref="JsonResponse"/>
	public JsonResponse(JsonNode node) => Body = node;

	/// <summary>
	/// Traverses data using path notation.
	/// <para><c>e.g some.deep.nested.json.path</c></para>
	/// <para>Supports bracket index syntax: <c>hits.hits.[0]._source</c></para>
	/// <para>Supports first/last: <c>hits.hits.[first()]._source</c>, <c>hits.hits._last_</c></para>
	/// <para>Supports arbitrary key: <c>some._arbitrary_key_.value</c></para>
	/// </summary>
	/// <param name="path">path into the stored object, keys are separated with a dot and the last key is returned as T</param>
	/// <typeparam name="T"></typeparam>
	/// <returns>T or default</returns>
	public T Get<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(string path) =>
		JsonNodePathTraversal.Get<T>(Body, path);
}
