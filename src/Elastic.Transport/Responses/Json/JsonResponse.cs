// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Text.Json.Nodes;

namespace Elastic.Transport;

/// <summary>
/// A response type backed by <see cref="JsonNode"/> from System.Text.Json.
///
/// <para>Provides direct DOM access via <see cref="TransportResponse{T}.Body"/> (e.g. <c>response.Body["hits"]["hits"][0]</c>)</para>
/// <para>Also exposes a safe path traversal mechanism via <see cref="JsonResponseBase.Get{T}"/> using dot-notation paths.</para>
/// </summary>
public sealed class JsonResponse : JsonResponseBase
{
	/// <inheritdoc cref="JsonResponse"/>
	public JsonResponse() { }

	/// <inheritdoc cref="JsonResponse"/>
	public JsonResponse(JsonNode node) : base(node) { }
}
