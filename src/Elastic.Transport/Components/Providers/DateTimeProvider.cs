// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;

namespace Elastic.Transport
{
	/// <summary>
	/// An abstraction to provide access to the current <see cref="DateTime"/>. This abstraction allows time to be tested within
	/// the transport.
	/// </summary>
	public abstract class DateTimeProvider
	{
		internal DateTimeProvider() { }

		/// <summary> The current date time </summary>
		public abstract DateTime Now();

		/// <summary>
		/// Calculate the dead time for a node based on the number of attempts.
		/// </summary>
		/// <param name="attempts">The number of attempts on the node</param>
		/// <param name="minDeadTimeout">The initial dead time as configured by <see cref="ITransportConfiguration.DeadTimeout"/></param>
		/// <param name="maxDeadTimeout">The configured maximum dead timeout as configured by <see cref="ITransportConfiguration.MaxDeadTimeout"/></param>
		public abstract DateTime DeadTime(int attempts, TimeSpan? minDeadTimeout, TimeSpan? maxDeadTimeout);
	}
}
