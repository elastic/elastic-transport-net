// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Elastic.Transport.Diagnostics.Auditing;
using Elastic.Transport.Extensions;

namespace Elastic.Transport
{
	/// <summary>
	/// A connection pool that enables <see cref="SupportsReseeding"/> which in turn allows the <see cref="ITransport{TConnectionSettings}"/> to enable sniffing to
	/// discover the current cluster's list of active nodes.
	/// </summary>
	public class SniffingConnectionPool : StaticConnectionPool
	{
		private readonly ReaderWriterLockSlim _readerWriter = new ReaderWriterLockSlim();

		/// <inheritdoc cref="SniffingConnectionPool"/>>
		public SniffingConnectionPool(IEnumerable<Uri> uris, bool randomize = true, IDateTimeProvider dateTimeProvider = null)
			: base(uris, randomize, dateTimeProvider) { }

		/// <inheritdoc cref="SniffingConnectionPool"/>>
		public SniffingConnectionPool(IEnumerable<Node> nodes, bool randomize = true, IDateTimeProvider dateTimeProvider = null)
			: base(nodes, randomize, dateTimeProvider) { }

		/// <inheritdoc cref="SniffingConnectionPool"/>>
		public SniffingConnectionPool(IEnumerable<Node> nodes, Func<Node, float> nodeScorer, IDateTimeProvider dateTimeProvider = null)
			: base(nodes, nodeScorer, dateTimeProvider) { }

		/// <inheritdoc />
		public override IReadOnlyCollection<Node> Nodes
		{
			get
			{
				try
				{
					//since internalnodes can be changed after returning we return
					//a completely new list of cloned nodes
					_readerWriter.EnterReadLock();
					return InternalNodes.Select(n => n.Clone()).ToList();
				}
				finally
				{
					_readerWriter.ExitReadLock();
				}
			}
		}

		/// <inheritdoc />
		public override bool SupportsPinging => true;

		/// <inheritdoc />
		public override bool SupportsReseeding => true;

		/// <inheritdoc />
		public override void Reseed(IEnumerable<Node> nodes)
		{
			if (!nodes.HasAny(out var nodesArray)) return;

			try
			{
				_readerWriter.EnterWriteLock();
				var sortedNodes = SortNodes(nodesArray)
					.DistinctBy(n => n.Uri)
					.ToList();

				InternalNodes = sortedNodes;
				GlobalCursor = -1;
				LastUpdate = DateTimeProvider.Now();
			}
			finally
			{
				_readerWriter.ExitWriteLock();
			}
		}

		/// <inheritdoc />
		public override IEnumerable<Node> CreateView(Action<AuditEvent, Node> audit = null)
		{
			_readerWriter.EnterReadLock();
			try
			{
				return base.CreateView(audit);
			}
			finally
			{
				_readerWriter.ExitReadLock();
			}
		}

		/// <summary> Allows subclasses to hook into the parents dispose </summary>
		protected override void DisposeManagedResources()
		{
			_readerWriter?.Dispose();
			base.DisposeManagedResources();
		}
	}
}
