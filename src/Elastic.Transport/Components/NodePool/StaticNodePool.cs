// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Elastic.Transport.Diagnostics.Auditing;
using Elastic.Transport.Extensions;

namespace Elastic.Transport;

/// <summary>
/// A node pool that disables <see cref="SupportsReseeding"/> which in turn disallows the <see cref="ITransport{TConfiguration}"/> to enable sniffing to
/// discover the current cluster's list of active nodes.
/// <para>Therefore the nodes you supply are the list of known nodes throughout its lifetime, hence static</para>
/// </summary>
public class StaticNodePool : NodePool
{
	private int _globalCursor = -1;

	/// <summary>
	/// Everytime <see cref="CreateView"/> is called it picks the initial starting point from this cursor.
	/// After which it uses a local cursor to commence the enumeration. This makes <see cref="CreateView"/> deterministic
	/// even when if multiple threads enumerate over multiple lazy collections returned by <see cref="CreateView"/>.
	/// </summary>
	protected ref int GlobalCursor => ref _globalCursor;

	private readonly Func<Node, float>? _nodeScorer;

	/// <inheritdoc cref="StaticNodePool"/>
	public StaticNodePool(IEnumerable<Uri> uris, bool randomize = true)
		: this(uris.Select(uri => new Node(uri)), randomize, null) { }

	/// <inheritdoc cref="StaticNodePool"/>
	public StaticNodePool(IEnumerable<Node> nodes, bool randomize = true)
		: this(nodes, randomize, null) { }

	/// <inheritdoc cref="StaticNodePool"/>
	protected StaticNodePool(IEnumerable<Node> nodes, bool randomize, int? randomizeSeed = null)
	{
		Randomize = randomize;
		Random = !randomize || !randomizeSeed.HasValue
			? new Random()
			: new Random(randomizeSeed.Value);

		InternalNodes = Initialize(nodes);
	}

	//this constructor is protected because nodeScorer only makes sense on subclasses that support reseeding otherwise just manually sort `nodes` before instantiating.
	/// <inheritdoc cref="StaticNodePool"/>
	protected StaticNodePool(IEnumerable<Node> nodes, Func<Node, float>? nodeScorer = null)
	{
		_nodeScorer = nodeScorer;
		InternalNodes = Initialize(nodes);
	}

	private List<Node> Initialize(IEnumerable<Node> nodes)
	{
		var nodesProvided = nodes.ToList() ?? throw new ArgumentNullException(nameof(nodes));
		nodesProvided.ThrowIfEmpty(nameof(nodes));

		string? scheme = null;
		foreach (var node in nodesProvided)
		{
			if (scheme == null)
			{
				scheme = node.Uri.Scheme;
				UsingSsl = scheme == "https";
			}
			else if (scheme != node.Uri.Scheme)
				// TODO - Diagnostic event here
				throw new ArgumentException("Trying to instantiate a node pool with mixed URI Schemes");
		}

		return SortNodes(nodesProvided)
			.DistinctByCustom(n => n.Uri)
			.ToList();
	}

	/// <inheritdoc />
	public override DateTimeOffset? LastUpdate { get; protected set; }

	/// <inheritdoc />
	public override int MaxRetries => InternalNodes.Count - 1;

	/// <inheritdoc />
	public override IReadOnlyCollection<Node> Nodes => InternalNodes;

	/// <inheritdoc />
	public override bool SupportsPinging => true;

	/// <inheritdoc />
	public override bool SupportsReseeding => false;

	/// <summary>
	/// A window into <see cref="InternalNodes"/> that only selects the nodes considered alive at the time of calling
	/// this property. Taking into account <see cref="DateTimeProvider.Now"/> and <see cref="Node.DeadUntil"/>
	/// </summary>
	protected IReadOnlyList<Node> AliveNodes
	{
		get
		{
			var now = DateTimeProvider.Now();
			return InternalNodes
				.Where(n => n.IsAlive || n.DeadUntil <= now)
				.ToList();
		}
	}

	/// <summary>
	/// The list of nodes we are operating over. This is protected so that subclasses that DO implement <see cref="SupportsReseeding"/>
	/// can update this list. Its up to subclasses to make this thread safe.
	/// </summary>
	protected List<Node> InternalNodes { get; set; }

	/// <summary>
	/// If <see cref="Randomize"/> is set sub classes that support reseeding will have to use this instance since it might be based of an
	/// explicit seed passed into the constructor.
	/// </summary>
	// ReSharper disable once MemberCanBePrivate.Global
	protected Random Random { get; } = new Random();

	/// <summary> Whether the nodes order should be randomized after sniffing </summary>
	// ReSharper disable once MemberCanBePrivate.Global
	protected bool Randomize { get; }

	/// <summary>
	/// Creates a view of all the live nodes with changing starting positions that wraps over on each call
	/// e.g Thread A might get 1,2,3,4,5 and thread B will get 2,3,4,5,1.
	/// if there are no live nodes yields a different dead node to try once
	/// </summary>
	public override IEnumerable<Node> CreateView(Auditor? auditor = null)
	{
		var nodes = AliveNodes;

		var globalCursor = Interlocked.Increment(ref GlobalCursor);

		if (nodes.Count == 0)
		{
			//could not find a suitable node retrying on first node off globalCursor
			yield return RetryInternalNodes(globalCursor, auditor);

			yield break;
		}

		var localCursor = globalCursor % nodes.Count;
		foreach (var aliveNode in SelectAliveNodes(localCursor, nodes, auditor)) yield return aliveNode;
	}

	/// <inheritdoc />
	public override void Reseed(IEnumerable<Node> nodes) { } //ignored


	/// <summary>
	/// If no active nodes are found this method can be used by subclasses to reactivate the next node based on
	/// <paramref name="globalCursor"/>
	/// </summary>
	/// <param name="globalCursor"></param>
	/// <param name="auditor">Trace action to document the fact all nodes were dead and were resurrecting one at random</param>
	protected Node RetryInternalNodes(int globalCursor, Auditor? auditor = null)
	{
		auditor?.Emit(AuditEvent.AllNodesDead);
		var node = InternalNodes[globalCursor % InternalNodes.Count];
		node.IsResurrected = true;
		auditor?.Emit(AuditEvent.Resurrection, node);
		return node;
	}

	/// <summary>
	/// Lazy enumerate <paramref name="aliveNodes"/> based on the local <paramref name="cursor"/>. Enumeration will start from <paramref name="cursor"/>
	/// and loop around the end and stop before hitting <paramref name="cursor"/> again. This ensures all nodes are attempted.
	/// </summary>
	/// <param name="cursor">The starting point into <paramref name="aliveNodes"/> from wich to start.</param>
	/// <param name="aliveNodes"></param>
	/// <param name="auditor">Trace action to notify if a resurrection occured</param>
	protected static IEnumerable<Node> SelectAliveNodes(int cursor, IReadOnlyList<Node> aliveNodes, Auditor? auditor = null)
	{
		// ReSharper disable once ForCanBeConvertedToForeach
		for (var attempts = 0; attempts < aliveNodes.Count; attempts++)
		{
			var node = aliveNodes[cursor];
			cursor = (cursor + 1) % aliveNodes.Count;
			//if this node is not alive or no longer dead mark it as resurrected
			if (!node.IsAlive)
			{
				auditor?.Emit(AuditEvent.Resurrection, node);
				node.IsResurrected = true;
			}

			yield return node;
		}
	}

	/// <summary>
	/// Provides the default sort order for <see cref="CreateView"/> this takes into account whether a subclass injected a custom <see cref="Node"/> comparer
	/// and if not whether <see cref="Randomize"/> is set
	/// </summary>
	protected IOrderedEnumerable<Node> SortNodes(IEnumerable<Node> nodes) =>
		_nodeScorer != null
			? nodes.OrderByDescending(_nodeScorer)
			: nodes.OrderBy(_ => Randomize ? Random.Next() : 1);

}
