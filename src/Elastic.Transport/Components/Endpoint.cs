// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;

namespace Elastic.Transport;

/// <summary>
/// Represents the path of an endpoint in a transport request, including the HTTP method
/// and the path and query information.
/// </summary>
/// <remarks>
/// This struct is used to store information about the HTTP method and the path and query of an endpoint,
/// which are essential components when constructing a request URI.
/// </remarks>
public readonly record struct EndpointPath(HttpMethod Method, string PathAndQuery);

/// <summary>
/// Represents an endpoint in a transport request, encapsulating the HTTP method, path and query,
/// and the node to which the request is being sent.
/// </summary>
/// <remarks>
/// This class is used to construct the URI for the request based on the node's URI and the path and query.
/// An empty endpoint can be created using the <see cref="Empty"/> method as a default or placeholder instance.
/// </remarks>
public record Endpoint(in EndpointPath Path, Node Node)
{
	/// <summary> Represents an empty endpoint used as a default or placeholder instance of <see cref="Endpoint"/>. </summary>
	public static Endpoint Empty(in EndpointPath path) => new(path, EmptyNode);

	private static readonly Node EmptyNode = new(new Uri("http://empty.example"));

	/// <summary> Indicates whether the endpoint is an empty placeholder instance. </summary>
	public bool IsEmpty => Node == EmptyNode;

	/// <summary> The <see cref="Uri" /> for the request. </summary>
	public Uri Uri { get; private init; } = new(Node.Uri, Path.PathAndQuery);

	/// <summary> The HTTP method used for the request (e.g., GET, POST, PUT, DELETE, HEAD). </summary>
	public HttpMethod Method => Path.Method;

	/// <summary> Gets the path and query of the endpoint.</summary>
	public string PathAndQuery => Path.PathAndQuery;

	/// <summary>
	/// Represents a node within the transport layer of the Elastic search client.
	/// This object encapsulates the characteristics of a node, allowing for comparisons and operations
	/// within the broader search infrastructure.
	/// </summary>
	public Node Node
	{
		get;
		init
		{
			field = value;
			Uri = new(Node.Uri, Path.PathAndQuery);
		}
	} = Node;

	/// <inheritdoc/>
	public override string ToString() => $"{Path.Method.GetStringValue()} {Uri}";

}
