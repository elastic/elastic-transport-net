// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;

namespace Elastic.Transport;

/// <inheritdoc cref="DateTimeProvider"/>
public sealed class DefaultDateTimeProvider : DateTimeProvider
{
	/// <summary> A static instance to reuse as <see cref="DefaultDateTimeProvider"/> is stateless </summary>
	public static readonly DefaultDateTimeProvider Default = new();
	private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(60);
	private static readonly TimeSpan MaximumTimeout = TimeSpan.FromMinutes(30);

	/// <inheritdoc cref="DateTimeProvider.DeadTime"/>
	public override DateTimeOffset DeadTime(int attempts, TimeSpan? minDeadTimeout, TimeSpan? maxDeadTimeout)
	{
		var timeout = minDeadTimeout.GetValueOrDefault(DefaultTimeout);
		var maxTimeout = maxDeadTimeout.GetValueOrDefault(MaximumTimeout);
		var milliSeconds = Math.Min(timeout.TotalMilliseconds * 2 * Math.Pow(2, (attempts * 0.5) - 1), maxTimeout.TotalMilliseconds);
		return Now().AddMilliseconds(milliSeconds);
	}

	/// <inheritdoc cref="DateTimeProvider.Now"/>
	public override DateTimeOffset Now() => DateTimeOffset.UtcNow;
}
