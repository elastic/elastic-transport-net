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
using System.Threading;
using Elastic.Transport.Diagnostics.Auditing;

namespace Elastic.Transport
{
	/// <summary>
	/// A connection pool implementation that does not support reseeding and stays on the first <see cref="Node"/> reporting true for <see cref="Node.IsAlive"/>.
	/// This is great if for instance you have multiple proxies that you can fallback on allowing you to seed the proxies in order of preference.
	/// </summary>
	public class StickyConnectionPool : StaticConnectionPool
	{
		/// <inheritdoc cref="StickyConnectionPool"/>
		public StickyConnectionPool(IEnumerable<Uri> uris, IDateTimeProvider dateTimeProvider = null)
			: base(uris, false, dateTimeProvider) { }

		/// <inheritdoc cref="StickyConnectionPool"/>
		public StickyConnectionPool(IEnumerable<Node> nodes, IDateTimeProvider dateTimeProvider = null)
			: base(nodes, false, dateTimeProvider) { }

		/// <inheritdoc cref="StaticConnectionPool.CreateView"/>
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

		/// <inheritdoc cref="StaticConnectionPool.Reseed"/>
		public override void Reseed(IEnumerable<Node> nodes) { }
	}
}
