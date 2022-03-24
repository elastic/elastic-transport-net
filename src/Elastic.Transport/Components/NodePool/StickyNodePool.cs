// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.Threading;
using Elastic.Transport.Diagnostics.Auditing;

namespace Elastic.Transport
{
	/// <summary>
	/// A connection pool implementation that does not support reseeding and stays on the first <see cref="Node"/> reporting true for <see cref="Node.IsAlive"/>.
	/// This is great if for instance you have multiple proxies that you can fallback on allowing you to seed the proxies in order of preference.
	/// </summary>
	public sealed class StickyNodePool : StaticNodePool
	{
		/// <inheritdoc cref="StickyNodePool"/>
		public StickyNodePool(IEnumerable<Uri> uris, IDateTimeProvider dateTimeProvider = null)
			: base(uris, false, dateTimeProvider) { }

		/// <inheritdoc cref="StickyNodePool"/>
		public StickyNodePool(IEnumerable<Node> nodes, IDateTimeProvider dateTimeProvider = null)
			: base(nodes, false, dateTimeProvider) { }

		/// <inheritdoc cref="StaticNodePool.CreateView"/>
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

		/// <inheritdoc cref="StaticNodePool.Reseed"/>
		public override void Reseed(IEnumerable<Node> nodes) { }
	}
}
