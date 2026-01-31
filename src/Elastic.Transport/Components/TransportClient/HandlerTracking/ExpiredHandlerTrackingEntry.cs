// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

#if !NETFRAMEWORK
using System;
using System.Net.Http;

namespace Elastic.Transport;

/// <summary>
/// Thread-safety: This class is immutable
/// <para>https://github.com/dotnet/runtime/blob/master/src/libraries/Microsoft.Extensions.Http/src/ExpiredHandlerTrackingEntry.cs</para>
/// </summary>
internal sealed class ExpiredHandlerTrackingEntry(ActiveHandlerTrackingEntry other)
{
	private readonly WeakReference _livenessTracker = new(other.Handler);

	// IMPORTANT: don't cache a reference to `other` or `other.Handler` here.
	// We need to allow it to be GC'ed.

	public bool CanDispose => !_livenessTracker.IsAlive;

	public HttpMessageHandler? InnerHandler { get; } = other.Handler.InnerHandler;

	public int Key { get; } = other.Key;
}
#endif
