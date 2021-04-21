// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using Elastic.Transport.Diagnostics.Auditing;

namespace Elastic.Transport
{
	/// <summary>
	/// A connection pool is responsible for maintaining a read only collection of <see cref="Node"/>(s) under <see cref="Nodes"/>.
	/// <para>
	/// Unlike the name might suggest this component is not responsible for IO level pooling. For that we rely on <see cref="IConnection"/> abstracting away
	/// the connection IO pooling.
	/// </para>
	/// <para>This interface signals the current connection strategy to <see cref="ITransport{TConnectionSettings}"/>.</para>
	/// </summary>
	public interface IConnectionPool : IDisposable
	{
		/// <summary>
		/// The last time that this instance was updated.
		/// </summary>
		DateTime LastUpdate { get; }

		/// <summary>
		/// Returns the default maximum retries for the connection pool implementation.
		/// Most implementations default to number of nodes, note that this can be overridden
		/// in the connection settings.
		/// </summary>
		int MaxRetries { get; }

		/// <summary>
		/// Returns a read only view of all the nodes in the cluster, which might involve creating copies of nodes e.g
		/// if you are using <see cref="SniffingConnectionPool" />.
		/// If you do not need an isolated copy of the nodes, please read <see cref="CreateView" /> to completion.
		/// </summary>
		IReadOnlyCollection<Node> Nodes { get; }

		/// <summary>
		/// Whether a sniff is seen on startup. The implementation is
		/// responsible for setting this in a thread safe fashion.
		/// </summary>
		bool SniffedOnStartup { get; set; }

		/// <summary>
		/// Whether pinging is supported.
		/// </summary>
		bool SupportsPinging { get; }

		/// <summary>
		/// Whether reseeding with new nodes is supported.
		/// </summary>
		bool SupportsReseeding { get; }

		/// <summary>
		/// Whether SSL/TLS is being used.
		/// </summary>
		bool UsingSsl { get; }

		/// <summary>
		/// Creates a view over the nodes, with changing starting positions, that wraps over on each call
		/// e.g Thread A might get 1,2,3,4,5 and thread B will get 2,3,4,5,1.
		/// if there are no live nodes yields a different dead node to try once
		/// </summary>
		IEnumerable<Node> CreateView(Action<AuditEvent, Node> audit = null);

		/// <summary>
		/// Reseeds the nodes. The implementation is responsible for thread safety.
		/// </summary>
		void Reseed(IEnumerable<Node> nodes);
	}
}
