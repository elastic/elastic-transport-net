// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;

namespace Elastic.Transport.VirtualizedCluster.Providers;

/// <inheritdoc cref="DateTimeProvider"/>
public sealed class TestableDateTimeProvider : DateTimeProvider
{
	private DateTime MutableNow { get; set; } = DateTime.UtcNow;

	/// <inheritdoc cref="DateTimeProvider.Now"/>
	public override DateTime Now() => MutableNow;

	/// <summary>
	/// Advance the time <see cref="Now"/> returns
	/// </summary>
	/// <param name="change">A fun that gets passed the current <see cref="Now"/> and needs to return the new value</param>
	public void ChangeTime(Func<DateTime, DateTime> change) => MutableNow = change(MutableNow);

	public override DateTime DeadTime(int attempts, TimeSpan? minDeadTimeout, TimeSpan? maxDeadTimeout) => throw new NotImplementedException();
}
