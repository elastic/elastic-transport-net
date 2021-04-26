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

namespace Elastic.Transport.VirtualizedCluster.Providers
{
	/// <inheritdoc cref="IDateTimeProvider"/>
	public class TestableDateTimeProvider : DateTimeProvider
	{
		private DateTime MutableNow { get; set; } = DateTime.UtcNow;

		/// <inheritdoc cref="IDateTimeProvider.Now"/>
		public override DateTime Now() => MutableNow;

		/// <summary>
		/// Advance the time <see cref="Now"/> returns
		/// </summary>
		/// <param name="change">A fun that gets passed the current <see cref="Now"/> and needs to return the new value</param>
		public void ChangeTime(Func<DateTime, DateTime> change) => MutableNow = change(MutableNow);
	}
}
