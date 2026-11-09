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

## API changes

- `Node.IsResurrected` is read-only. The pool is the only thing that decides a node is due a ping.
- `Node` health is exposed through `IsAlive`, `IsResurrected`, `DeadUntil` and `FailedAttempts` as before, but they are
  now read from one atomic state, so you can no longer observe a half-updated node.
- `RequestPipeline` and `RequestPipelineFactory` are internal, and so is `RequestAttempt`. They were never a usable
  extension point (the factory's constructor was already internal), and nothing in this organisation's repositories
  referenced them. `ITransportConfiguration.PipelineProvider` and `TransportConfigurationDescriptor.PipelineProvider()`
  are gone with them.
- `SniffingNodePool` no longer overrides `CreateView` (it held a lock around a lazy iterator, which guarded nothing).
- `netstandard2.1` is no longer a separate target. `netstandard2.0` consumers are unaffected, as are `net8.0` and later.
- `Elastic.Transport.VirtualizedCluster`: `SingleNodeConnection()` works, `ClientThrows()` applies per request, and a
  sniff rule without a new cluster state no longer throws.

## Building

The repository pins the .NET 11 SDK (`global.json`) and builds the libraries for `netstandard2.0`, `net462`, `net8.0`,
`net10.0` and `net11.0`. C# 15 `union` types are used internally; a small internal polyfill supplies the two runtime
types below `net11.0`, so they do not appear in the public API.
