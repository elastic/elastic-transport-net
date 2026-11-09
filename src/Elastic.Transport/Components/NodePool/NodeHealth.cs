// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;

namespace Elastic.Transport;

// A node's health used to be four loosely coupled fields (IsAlive, IsResurrected, DeadUntil, FailedAttempts) that
// were written separately, so readers could observe combinations that never occur and `FailedAttempts++` raced.
// Only four combinations are meaningful, so each is a named state that carries exactly its own data:
//
//   Untested ──MarkDead──▶ Dead ──pool revisits──▶ Resurrected ──MarkAlive──▶ Alive
//      │                    ▲  ▲                        │                       │  ▲
//      └──────MarkAlive─────┼──┼────────────────────────┴──────MarkDead────────┘  │
//                           │  └────────────────────────────────────────────────────┘ (Alive ──pool revisits──▶ Untested)
//
// `Untested` is a node we have not yet pinged (alive, but IsResurrected): every new node starts here.

/// <summary>A node that has not proven itself yet: treated as alive, but pinged before its first use.</summary>
internal sealed record NodeUntested
{
	public static readonly NodeUntested Instance = new();
}

/// <summary>A node that answered its ping or a request.</summary>
internal sealed record NodeAlive
{
	public static readonly NodeAlive Instance = new();
}

/// <summary>A node taken out of rotation until <see cref="Until"/> after <see cref="FailedAttempts"/> consecutive failures.</summary>
internal sealed record NodeDead(DateTimeOffset Until, int FailedAttempts);

/// <summary>A dead node whose time is up (or that the pool is retrying because everything is dead): it gets a ping before use.</summary>
internal sealed record NodeResurrected(DateTimeOffset Until, int FailedAttempts);

internal union NodeHealth(NodeUntested, NodeAlive, NodeDead, NodeResurrected)
{
	public bool IsAlive => this switch
	{
		NodeUntested or NodeAlive => true,
		NodeDead or NodeResurrected => false,
	};

	public bool IsResurrected => this switch
	{
		NodeUntested or NodeResurrected => true,
		NodeAlive or NodeDead => false,
	};

	public int FailedAttempts => this switch
	{
		NodeUntested or NodeAlive => 0,
		NodeDead d => d.FailedAttempts,
		NodeResurrected r => r.FailedAttempts,
	};

	public DateTimeOffset DeadUntil => this switch
	{
		NodeUntested or NodeAlive => default,
		NodeDead d => d.Until,
		NodeResurrected r => r.Until,
	};

	/// <summary>A request to a node failed: it goes (back) out of rotation, one failure further.</summary>
	public NodeHealth MarkDead(DateTimeOffset until) => new NodeDead(until, FailedAttempts + 1);

	/// <summary>A request or ping succeeded: the slate is wiped clean.</summary>
	public NodeHealth MarkAlive() => NodeAlive.Instance;

	/// <summary>
	/// The pool is about to hand this node out again. A dead node becomes resurrected (keeping its history, so
	/// backoff continues if the retry fails); a verified-alive node is merely due another ping.
	/// </summary>
	public NodeHealth Resurrect() => this switch
	{
		NodeAlive => NodeUntested.Instance,
		NodeDead d => new NodeResurrected(d.Until, d.FailedAttempts),
		NodeUntested u => u,
		NodeResurrected r => r,
	};
}
