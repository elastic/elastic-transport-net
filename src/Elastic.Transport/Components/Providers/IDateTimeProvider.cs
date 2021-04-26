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

namespace Elastic.Transport
{
	/// <summary>
	/// An abstraction to provide access to the current <see cref="DateTime"/>. This abstraction allows time to be tested within
	/// the transport.
	/// </summary>
	public interface IDateTimeProvider
	{
		/// <summary> The current date time </summary>
		DateTime Now();

		/// <summary>
		/// Calculate the dead time for a node based on the number of attempts.
		/// </summary>
		/// <param name="attempts">The number of attempts on the node</param>
		/// <param name="minDeadTimeout">The initial dead time as configured by <see cref="ITransportConfiguration.DeadTimeout"/></param>
		/// <param name="maxDeadTimeout">The configured maximum dead timeout as configured by <see cref="ITransportConfiguration.MaxDeadTimeout"/></param>
		DateTime DeadTime(int attempts, TimeSpan? minDeadTimeout, TimeSpan? maxDeadTimeout);
	}
}
