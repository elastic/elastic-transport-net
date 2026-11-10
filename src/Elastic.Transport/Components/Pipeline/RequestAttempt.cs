// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

namespace Elastic.Transport;

/// <summary>
/// The state of one request as it walks the nodes. It belongs to the request, never to the (shared) pipeline:
/// two concurrent requests must not see each other's attempts or reseeds.
/// </summary>
internal sealed class RequestAttempt
{
	/// <summary>The number of nodes this request has attempted so far. Counts against <see cref="IRequestConfiguration.MaxRetries"/>.</summary>
	public int AttemptedNodes { get; set; }

	/// <summary>
	/// Set when a sniff during this request reseeded the node pool, so the node walk must start again from a fresh view.
	/// </summary>
	public bool RefreshRequested { get; set; }

	/// <summary>The recoverable failures seen so far, in order. Null until the first one, so a clean request allocates nothing.</summary>
	public System.Collections.Generic.List<PipelineException>? SeenExceptions { get; set; }

	// The walk over the nodes (see RequestPipeline.MoveNext). It used to be a compiler-generated iterator, a second
	// object per request next to this one; holding its state here makes it one.

	/// <summary>The node offered by the last successful <c>MoveNext</c>.</summary>
	public Node? Current { get; set; }

	/// <summary>The enumerator over the current view of the pool, or null between views.</summary>
	public System.Collections.Generic.IEnumerator<Node>? View { get; set; }

	/// <summary>How many views of the pool this request has walked; a reseed starts another, up to a limit.</summary>
	public int Views { get; set; }

	/// <summary>True once the one node of a forced request has been offered.</summary>
	public bool ForcedNodeOffered { get; set; }

	/// <summary>Ends the current view, if there is one.</summary>
	public void EndView()
	{
		View?.Dispose();
		View = null;
	}
}
