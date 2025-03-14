// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

#if !NETFRAMEWORK
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;

namespace Elastic.Transport;

/// <summary>
/// Heavily modified version of DefaultHttpClientFactory, re-purposed for BoundConfiguration
/// <para>https://github.com/dotnet/runtime/blob/master/src/libraries/Microsoft.Extensions.Http/src/DefaultHttpClientFactory.cs</para>
/// </summary>
internal sealed class BoundConfigurationHttpClientFactory : IDisposable
{
	private readonly Func<BoundConfiguration, HttpMessageHandler> _createHttpClientHandler;
	private static readonly TimerCallback CleanupCallback = (s) => ((BoundConfigurationHttpClientFactory)s).CleanupTimer_Tick();
	private readonly Func<int, BoundConfiguration, Lazy<ActiveHandlerTrackingEntry>> _entryFactory;

	// Default time of 10s for cleanup seems reasonable.
	// Quick math:
	// 10 distinct named clients * expiry time >= 1s = approximate cleanup queue of 100 items
	//
	// This seems frequent enough. We also rely on GC occurring to actually trigger disposal.
	private readonly TimeSpan _defaultCleanupInterval = TimeSpan.FromSeconds(10);

	// We use a new timer for each regular cleanup cycle, protected with a lock. Note that this scheme
	// doesn't give us anything to dispose, as the timer is started/stopped as needed.
	//
	// There's no need for the factory itself to be disposable. If you stop using it, eventually everything will
	// get reclaimed.
	private Timer _cleanupTimer;
	private readonly object _cleanupTimerLock;
	private readonly object _cleanupActiveLock;

	// Collection of 'active' handlers.
	//
	// Using lazy for synchronization to ensure that only one instance of HttpMessageHandler is created
	// for each name.
	//
	private readonly ConcurrentDictionary<int, Lazy<ActiveHandlerTrackingEntry>> _activeHandlers;

	/// <summary>
	/// The total number if in use message handlers, this number should not exceed the double digits
	/// for any default setup
	/// </summary>
	public int InUseHandlers => _activeHandlers.Count;

	private int _removedHandlers;
	/// <summary>
	/// The total amount of handlers that have been swapped over during the lifetime of the application.
	/// This can be high depending on how aggressive the DNS caching is setup.
	/// </summary>
	public int RemovedHandlers => _removedHandlers;

	// Collection of 'expired' but not yet disposed handlers.
	//
	// Used when we're rotating handlers so that we can dispose HttpMessageHandler instances once they
	// are eligible for garbage collection.
	//
	private readonly ConcurrentQueue<ExpiredHandlerTrackingEntry> _expiredHandlers;
	private readonly TimerCallback _expiryCallback;

	public BoundConfigurationHttpClientFactory(Func<BoundConfiguration, HttpMessageHandler> createHttpClientHandler)
	{
		_createHttpClientHandler = createHttpClientHandler;
		// case-sensitive because named options is.
		_activeHandlers = new ConcurrentDictionary<int, Lazy<ActiveHandlerTrackingEntry>>();
		_entryFactory = (key, boundConfiguration) =>
		{
			return new Lazy<ActiveHandlerTrackingEntry>(() => CreateHandlerEntry(key, boundConfiguration),
				LazyThreadSafetyMode.ExecutionAndPublication);
		};

		_expiredHandlers = new ConcurrentQueue<ExpiredHandlerTrackingEntry>();
		_expiryCallback = ExpiryTimer_Tick;

		_cleanupTimerLock = new object();
		_cleanupActiveLock = new object();
	}

	public HttpClient CreateClient(BoundConfiguration boundConfiguration)
	{
		if (boundConfiguration == null) throw new ArgumentNullException(nameof(boundConfiguration));

		var key = HttpRequestInvoker.GetClientKey(boundConfiguration);
		var handler = CreateHandler(key, boundConfiguration);
		var client = new HttpClient(handler, disposeHandler: false)
		{
			Timeout = boundConfiguration.RequestTimeout
		};
		return client;
	}

	private HttpMessageHandler CreateHandler(int key, BoundConfiguration boundConfiguration)
	{
		if (boundConfiguration == null) throw new ArgumentNullException(nameof(boundConfiguration));

#if !NETSTANDARD2_0 && !NETFRAMEWORK
		var entry = _activeHandlers.GetOrAdd(key, (k, r) => _entryFactory(k, r), boundConfiguration).Value;
#else
		var entry = _activeHandlers.GetOrAdd(key, (k) => _entryFactory(k, boundConfiguration)).Value;
#endif

		StartHandlerEntryTimer(entry);

		return entry.Handler;
	}

	private ActiveHandlerTrackingEntry CreateHandlerEntry(int key, BoundConfiguration boundConfiguration)
	{
		// Wrap the handler so we can ensure the inner handler outlives the outer handler.
		var handler = new LifetimeTrackingHttpMessageHandler(_createHttpClientHandler(boundConfiguration));

		// Note that we can't start the timer here. That would introduce a very very subtle race condition
		// with very short expiry times. We need to wait until we've actually handed out the handler once
		// to start the timer.
		//
		// Otherwise it would be possible that we start the timer here, immediately expire it (very short
		// timer) and then dispose it without ever creating a client. That would be bad. It's unlikely
		// this would happen, but we want to be sure.
		return new ActiveHandlerTrackingEntry(key, handler, boundConfiguration.DnsRefreshTimeout);
	}

	private void ExpiryTimer_Tick(object state)
	{
		var active = (ActiveHandlerTrackingEntry)state;

		// The timer callback should be the only one removing from the active collection. If we can't find
		// our entry in the collection, then this is a bug.
		var removed = _activeHandlers.TryRemove(active.Key, out var found);
		if (removed)
			Interlocked.Increment(ref _removedHandlers);
		Debug.Assert(removed, "Entry not found. We should always be able to remove the entry");
		// ReSharper disable once RedundantNameQualifier
		Debug.Assert(object.ReferenceEquals(active, found.Value), "Different entry found. The entry should not have been replaced");

		// At this point the handler is no longer 'active' and will not be handed out to any new clients.
		// However we haven't dropped our strong reference to the handler, so we can't yet determine if
		// there are still any other outstanding references (we know there is at least one).
		//
		// We use a different state object to track expired handlers. This allows any other thread that acquired
		// the 'active' entry to use it without safety problems.
		var expired = new ExpiredHandlerTrackingEntry(active);
		_expiredHandlers.Enqueue(expired);

		StartCleanupTimer();
	}

	private void StartHandlerEntryTimer(ActiveHandlerTrackingEntry entry) => entry.StartExpiryTimer(_expiryCallback);

	private void StartCleanupTimer()
	{
		lock (_cleanupTimerLock)
			_cleanupTimer ??= NonCapturingTimer.Create(CleanupCallback, this, _defaultCleanupInterval, Timeout.InfiniteTimeSpan);
	}

	private void StopCleanupTimer()
	{
		lock (_cleanupTimerLock)
		{
			_cleanupTimer?.Dispose();
			_cleanupTimer = null;
		}
	}

	private void CleanupTimer_Tick()
	{
		// Stop any pending timers, we'll restart the timer if there's anything left to process after cleanup.
		//
		// With the scheme we're using it's possible we could end up with some redundant cleanup operations.
		// This is expected and fine.
		//
		// An alternative would be to take a lock during the whole cleanup process. This isn't ideal because it
		// would result in threads executing ExpiryTimer_Tick as they would need to block on cleanup to figure out
		// whether we need to start the timer.
		StopCleanupTimer();

		if (!Monitor.TryEnter(_cleanupActiveLock))
		{
			// We don't want to run a concurrent cleanup cycle. This can happen if the cleanup cycle takes
			// a long time for some reason. Since we're running user code inside Dispose, it's definitely
			// possible.
			//
			// If we end up in that position, just make sure the timer gets started again. It should be cheap
			// to run a 'no-op' cleanup.
			StartCleanupTimer();
			return;
		}

		try
		{
			var initialCount = _expiredHandlers.Count;

			for (var i = 0; i < initialCount; i++)
			{
				// Since we're the only one removing from _expired, TryDequeue must always succeed.
				_expiredHandlers.TryDequeue(out var entry);
				Debug.Assert(entry != null, "Entry was null, we should always get an entry back from TryDequeue");

				if (entry.CanDispose)
				{
					try
					{
						entry.InnerHandler.Dispose();
					}
					catch (Exception)
					{
						// ignored (ignored in HttpClientFactory too)
					}
				}
				// If the entry is still live, put it back in the queue so we can process it
				// during the next cleanup cycle.
				else
					_expiredHandlers.Enqueue(entry);
			}
		}
		finally
		{
			Monitor.Exit(_cleanupActiveLock);
		}

		// We didn't totally empty the cleanup queue, try again later.
		if (_expiredHandlers.Count > 0) StartCleanupTimer();
	}

	public void Dispose()
	{
		//try to cleanup nicely
		CleanupTimer_Tick();
		_cleanupTimer?.Dispose();

		//CleanupTimer might not cleanup everything because it will only dispose if the WeakReference allows it.
		// here we forcefully dispose a Client -> ConnectionSettings -> Connection -> BoundConfigurationHttpClientFactory
		var attempts = 0;
		do
		{
			attempts++;
			var initialCount = _expiredHandlers.Count;
			for (var i = 0; i < initialCount; i++)
			{
				// Since we're the only one removing from _expired, TryDequeue must always succeed.
				_expiredHandlers.TryDequeue(out var entry);
				try
				{
					entry?.InnerHandler.Dispose();
				}
				catch (Exception)
				{
					// ignored (ignored in HttpClientFactory too)
				}
			}
		} while (attempts < 5 && _expiredHandlers.Count > 0);

	}
}
#endif
