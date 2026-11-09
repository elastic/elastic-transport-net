# Moving from 1.x to 2.0

Elastic.Transport 2.0 fixes a set of defects in how a request walks its nodes, and tightens a few public types that
should never have been mutable. If you only use `ITransport`, `TransportConfiguration` and the node pools, most of this
will not touch you, but the behaviour fixes can change results, so read that section.

## Behaviour changes

- **`MaxRetries` is enforced.** A request used to try every node in the pool whatever `MaxRetries` said. A request now
  makes at most `min(MaxRetries, nodes - 1) + 1` attempts.
- **Request-level settings win.** `UserAgent`, `ForceNode`, `DisableSniff`, `EnableHttpCompression`, `MaxRetries` and
  `DisablePings` set on a single request now override the transport's value. Before, the transport's value won for the
  first four, and the request's value was ignored for the last two. A transport-level `ContentType` is also honoured.
- **`MaxRetryTimeout` is honoured.** It was accepted and ignored. It now bounds the whole request including retries,
  and defaults to `RequestTimeout` as documented.
- **Sniffing tries every node.** It used to give up after the first node that failed.
- **Stale-cluster sniffing can fire.** `NodePool.LastUpdate` starts at the pool's creation time instead of `null`.
- **Error bodies.** `ApiCallDetails.ResponseBodyInBytes` holds the body of every non-success response, including HTML
  from a proxy, even when `SkipDeserializationForStatusCodes` applies. (This was already the behaviour of 1.x; the old
  7.x tests asserted otherwise.)

- **Connection pools are keyed on what a handler is built from.** Two requests share an HTTP handler (and so a connection
  pool) exactly when their compression, proxy, DNS refresh, certificates and transport settings are the same. Before,
  the key was a hash that could collide across different proxy credentials, ignored several of those settings, and
  included `RequestTimeout`, so each distinct per-request timeout opened its own pool.
- **Byte-array bodies are not copied when nothing transforms them** (`PostData.Bytes` with request compression off).
- **Pooled streams come from `Microsoft.IO.RecyclableMemoryStream`.** `RecyclableMemoryStreamFactory` is unchanged, but its
  ETW event source is now the package's (`Microsoft-IO-RecyclableMemoryStream`) and the package depends on `System.Memory`
  below net8.0. The default `MemoryStreamFactory` is still a plain `MemoryStream`, on purpose: a pool keeps memory for reuse,
  which a memory dump of a healthy application then shows, and people read that as a leak. Pooling is opt-in
  (`MemoryStreamFactory = new RecyclableMemoryStreamFactory(blockSize: 16 * 1024)`), and worth it for large bodies that are
  serialized and not compressed: for a 64 KB body that is about 7 KB allocated per request against about 204 KB, with
  gen 2 collections, for a plain stream. With compression on, the default, the two are about equal.
  `RecyclableMemoryStreamFactory` is now **bounded**: by default it keeps at most 50 MB idle
  (`RecyclableMemoryStreamFactory.DefaultMaximumFreeBytes`), and buffers returned beyond that are left to the garbage
  collector, so what a memory dump shows as pooled has a ceiling. Before, the pool kept its high-water mark forever. Choose
  your own with `new RecyclableMemoryStreamFactory(blockSize, maximumFreeBytes)`. Memory held by streams that are in use is
  not counted. The pool helps large buffers and does nothing for small ones.

## API changes

- `Node.IsResurrected` is read-only. The pool is the only thing that decides a node is due a ping.
- `Node` health is exposed through `IsAlive`, `IsResurrected`, `DeadUntil` and `FailedAttempts` as before, but they are
  now read from one atomic state, so you can no longer observe a half-updated node.
- `RequestPipeline` and `RequestPipelineFactory` are internal, and so is `RequestAttempt`. They were never a usable
  extension point (the factory's constructor was already internal), and nothing in this organisation's repositories
  referenced them. `ITransportConfiguration.PipelineProvider` and `TransportConfigurationDescriptor.PipelineProvider()`
  are gone with them.
- `Elastic.Transport.VirtualizedCluster`: `ExposingPipelineFactory<T>` is internal. It existed to hand out a
  `RequestPipeline` from the test-support package, and was the only public way to obtain one; it never registered itself
  with the transport, so it had no effect on how requests ran.
- `RuleOption<TFirst, TSecond>` (VirtualizedCluster) is a C# 15 union, so an absent option is `RuleOption<...>?` rather than
  `null`. Rules still read `Fails(Times(2), 503)` and `FailAlways(new Exception())`.
- `Audit` is a record.
- `SniffingNodePool` no longer overrides `CreateView` (it held a lock around a lazy iterator, which guarded nothing).
- `netstandard2.1` is no longer a separate target. `netstandard2.0` consumers are unaffected, as are `net8.0` and later.
- `Elastic.Transport.VirtualizedCluster`: `SingleNodeConnection()` works, `ClientThrows()` applies per request, and a
  sniff rule without a new cluster state no longer throws.

## Building

The repository pins the .NET 11 SDK (`global.json`) and builds the libraries for `netstandard2.0`, `net462`, `net8.0`,
`net10.0` and `net11.0`. C# 15 `union` types are used internally; a small internal polyfill supplies the two runtime
types below `net11.0`, so they do not appear in the public API.
