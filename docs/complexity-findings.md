# Where the complexity is, and what to do about it

An investigation of `Elastic.Transport` (18.6k lines, 124 public types) for complexity that earns nothing. Every item below was
measured first. Where a change was safe it was made and proved; where it would remove or change public API it is listed as a
decision, with the evidence, and left alone.

## Done

| Item | Finding | Result |
|---|---|---|
| **Conditional compilation** | 55 `#if` directives in 29 files used 22 spellings (`NET6_0_OR_GREATER`, `NET8_0_OR_GREATER`, `NETSTANDARD2_1_OR_GREATER \|\| NET`, ...) that all mean one thing for the five targets | Rewritten by truth table to 12. All five compiled assemblies are byte-identical before and after (an unchanged rebuild is also byte-identical, so the comparison is sound) |
| **Sync/async twins in `HttpRequestInvoker`** | `SetContent` and `SetContentAsync` shared 27 of their 32 and 36 lines | Two shared helpers; allocations identical to the byte, timing within noise |
| **A bug in `SemVersion`** | Found by a property test: `CompareByPrecedence` ended with a comparison of `this.Prerelease` against `other.Metadata`, so two *equal* pre-release versions compared as different | Fixed; build metadata no longer affects precedence (semver 2.0 section 10) |
| **The pipeline types** | `RequestPipeline`, its factory and `PipelineProvider` were public but could not be constructed or subclassed from outside | Made internal (see the migration notes) |
| **The vendored `RecyclableMemoryStream`** | 1,833 lines of a 2015 copy | Replaced by the package: 2.2x faster pooled, same allocation |

## Safety nets that now exist (CsCheck, 55 properties, all mutation-checked)

These are what make the decisions below safe to take later: precedence of every setting, per-request state isolation, handler keys,
header merging, node health, pool views, a stateful model of the pool over time, UTF-8 streaming, the audit trail, every
serializer overload on both code paths, `SemVersion`, back-off, `PostData`, query strings, cloud ids, sniff parsing, and
`DynamicResponse` against `JsonResponse`.

## Decisions for you

### 1. Configuration sprawl: leave, unless settings are about to be added in bulk
Six types, 2,086 lines (`IRequestConfiguration`, `ITransportConfiguration`, `RequestConfiguration`, `RequestConfigurationDescriptor`,
`TransportConfiguration`, `TransportConfigurationDescriptor`, plus `BoundConfiguration`). Adding one setting touches **8 files and
about 21 places** (checked with `EnableTcpStats`). A source generator for the descriptors and copy constructors would remove most
of that, at the price of a generator to maintain, and `RequestConfiguration`'s copy constructor cannot become `with` because it
copies from an interface. Settings are added rarely, so the saving is real but small. The precedence property is the net if it is
ever done. **Recommendation: leave.**

### 2. net462: keep in this change, drop in a separate one
.NET Framework 4.6.2 is out of support on 12 January 2027. It costs `HttpWebRequestInvoker` (480 lines, with 10 sync/async twin
branches), 9 `NETFRAMEWORK` and 15 `!NETFRAMEWORK` blocks, and cannot be tested on macOS (only built; Windows CI runs it). Dropping it
would remove all of that. **Decision made: keep it here; consider dropping it as its own follow-up.**

### 3. `SemVersion` and `VersionInfo`: made internal (decided)
`SemVersion` (321 lines) was in the public constructor of `VersionInfo`, and nobody in seven Elastic .NET repositories derives
`VersionInfo` or uses `SemVersion`; it also had a precedence bug. Both are now internal. One thing the usage count missed:
`ReflectionVersionInfo` derives from `VersionInfo` and `elastic-ingest-dotnet` exposes one in a public field, so it stays public as a
standalone type with the same members. The real `LibraryVersion.cs` (ingest) and `EsqlProductRegistration.cs` (esql) compile against it
verbatim.

### 4. Public types nobody uses
124 public types; one is unreachable by API. Twelve have no use in any of seven repositories: `Base64ApiKey`,
`DefaultDateTimeProvider`, `DefaultMetaHeaderProducer`, `ErrorConverter`, `MetaDataHeader`, `SniffParser`, `TestableResponseFactory`,
`ThreadPoolStats`, `TransportExtensions`, `TypedDiagnosticObserver<,>`, `TypedResponseBuilder<T>`, `HttpWebRequestInvoker`. Several
are deliberate (`TypedResponseBuilder<T>` is documented in the README as the way to add response types; `DefaultDateTimeProvider` and
`TestableResponseFactory` are for tests). A count is not a reason to remove a public type. **Recommendation: review the list and
tell me which are meant to be public;** I will not prune on usage alone.

### 5. The serializer overloads: leave
`TransportSerializerExtensions` is 767 lines and 25 public methods; 19 carry the same fast-path/fallback pair. Merging the public
overloads is a breaking change, and each generic `T` form exists for AOT, so they cannot collapse into the `Type` forms. Factoring
the shared bodies would save perhaps 150 lines at some cost in readability. **Recommendation: leave.** The properties (every
overload, both code paths) make a later change safe.

### 6. Dynamic responses: keep
1,592 lines (`DynamicValue`, `DynamicDictionary`, `DynamicResponse`). It is AOT-compatible (the repo's own `PublishAot` example passes
its `DynamicResponse` tests as a native binary with no warnings); only member access through the C# `dynamic` keyword needs dynamic
code. One downstream reference. **Recommendation: keep.** `DynamicResponse` is now checked against `JsonResponse` for any JSON.

### 7. Elasticsearch-specific code in the core: leave
`Products/Elasticsearch` is 1,657 lines, 8% of the library. Moving it to its own package is a structural change with real breakage
for every consumer and no measured benefit. **Recommendation: leave.**
