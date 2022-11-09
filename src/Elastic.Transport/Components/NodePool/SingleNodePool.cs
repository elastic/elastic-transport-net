// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using Elastic.Transport.Diagnostics.Auditing;

namespace Elastic.Transport
{
	/// <summary> A pool to a single node or endpoint.</summary>
	public class SingleNodePool : NodePool
	{
		/// <inheritdoc cref="SingleNodePool"/>
		public SingleNodePool(Uri uri, DateTimeProvider dateTimeProvider = null)
		{
			var node = new Node(uri);
			UsingSsl = node.Uri.Scheme == "https";
			Nodes = new List<Node> { node };
			LastUpdate = (dateTimeProvider ?? DefaultDateTimeProvider.Default).Now();
		}

		/// <inheritdoc />
		public override DateTime LastUpdate { get; protected set; }

		/// <inheritdoc />
		public override int MaxRetries => 0;

		/// <inheritdoc />
		public override IReadOnlyCollection<Node> Nodes { get; }

		/// <inheritdoc />
		public override bool SupportsPinging => false;

		/// <inheritdoc />
		public override bool SupportsReseeding => false;

		/// <inheritdoc />
		public override bool UsingSsl { get; protected set; }

		/// <inheritdoc />
		public override IEnumerable<Node> CreateView(Action<AuditEvent, Node> audit = null) => Nodes;

		/// <inheritdoc />
		public override void Reseed(IEnumerable<Node> nodes) { } //ignored

		/// <inheritdoc />
		protected override void Dispose(bool disposing) => base.Dispose(disposing);
	}
}
