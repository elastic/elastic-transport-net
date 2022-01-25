// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Elastic.Transport.Diagnostics.Auditing;

namespace Elastic.Transport
{
	/// <summary>
	/// A connection pool implementation that supports reseeding but stays on the first <see cref="Node"/> reporting true for <see cref="Node.IsAlive"/>.
	/// This is great if for instance you have multiple proxies that you can fallback on allowing you to seed the proxies in order of preference.
	/// </summary>
	public class StickySniffingNodePool : SniffingNodePool
	{
		/// <inheritdoc cref="StickySniffingNodePool"/>
		public StickySniffingNodePool(IEnumerable<Uri> uris, Func<Node, float> nodeScorer, IDateTimeProvider dateTimeProvider = null)
			: base(uris.Select(uri => new Node(uri)), nodeScorer ?? DefaultNodeScore, dateTimeProvider) { }

		/// <inheritdoc cref="StickySniffingNodePool"/>
		public StickySniffingNodePool(IEnumerable<Node> nodes, Func<Node, float> nodeScorer, IDateTimeProvider dateTimeProvider = null)
			: base(nodes, nodeScorer ?? DefaultNodeScore, dateTimeProvider) { }

		/// <inheritdoc cref="NodePool.SupportsPinging"/>
		public override bool SupportsPinging => true;

		/// <inheritdoc cref="NodePool.SupportsReseeding"/>
		public override bool SupportsReseeding => true;

		/// <inheritdoc cref="NodePool.CreateView"/>
		public override IEnumerable<Node> CreateView(Action<AuditEvent, Node> audit = null)
		{
			var nodes = AliveNodes;

			if (nodes.Count == 0)
			{
				var globalCursor = Interlocked.Increment(ref GlobalCursor);

				//could not find a suitable node retrying on first node off globalCursor
				yield return RetryInternalNodes(globalCursor, audit);

				yield break;
			}

			// If the cursor is greater than the default then it's been
			// set already but we now have a live node so we should reset it
			if (GlobalCursor > -1)
				Interlocked.Exchange(ref GlobalCursor, -1);

			var localCursor = 0;
			foreach (var aliveNode in SelectAliveNodes(localCursor, nodes, audit))
				yield return aliveNode;
		}

		/// <summary> Allows subclasses to hook into the parents dispose </summary>
		private static float DefaultNodeScore(Node node) => 0f;
	}
}
