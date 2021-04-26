/*
 * Licensed to Elasticsearch B.V. under one or more contributor
 * license agreements. See the NOTICE file distributed with
 * this work for additional information regarding copyright
 * ownership. Elasticsearch B.V. licenses this file to you under
 * the Apache License, Version 2.0 (the "License"); you may
 * not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *    http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing,
 * software distributed under the License is distributed on an
 * "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
 * KIND, either express or implied.  See the License for the
 * specific language governing permissions and limitations
 * under the License.
 */

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
	public class StickySniffingConnectionPool : SniffingConnectionPool
	{
		/// <inheritdoc cref="StickySniffingConnectionPool"/>
		public StickySniffingConnectionPool(IEnumerable<Uri> uris, Func<Node, float> nodeScorer, IDateTimeProvider dateTimeProvider = null)
			: base(uris.Select(uri => new Node(uri)), nodeScorer ?? DefaultNodeScore, dateTimeProvider) { }

		/// <inheritdoc cref="StickySniffingConnectionPool"/>
		public StickySniffingConnectionPool(IEnumerable<Node> nodes, Func<Node, float> nodeScorer, IDateTimeProvider dateTimeProvider = null)
			: base(nodes, nodeScorer ?? DefaultNodeScore, dateTimeProvider) { }

		/// <inheritdoc cref="IConnectionPool.SupportsPinging"/>
		public override bool SupportsPinging => true;

		/// <inheritdoc cref="IConnectionPool.SupportsReseeding"/>
		public override bool SupportsReseeding => true;

		/// <inheritdoc cref="IConnectionPool.CreateView"/>
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
