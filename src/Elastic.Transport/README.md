# Elastic.Transport

Transport classes and utilities shared among .NET Elastic client libraries. Provides cluster-aware, resilient HTTP transport optimized for the Elastic product suite and Elastic Cloud.

## Installation

```
dotnet add package Elastic.Transport
```

## Quick Start

```csharp
var settings = new TransportConfiguration(new Uri("http://localhost:9200"));
var transport = new DistributedTransport(settings);

// GET request — returns body as string
var response = transport.Get<StringResponse>("/my-index/_search?q=title:hello");

// POST request — send JSON body
var body = PostData.String(@"{ ""query"": { ""match_all"": {} } }");
var searchResponse = transport.Post<StringResponse>("/my-index/_search", body);

// HEAD request — no body needed
var headResponse = transport.Head("/my-index");

// Async variants
var asyncResponse = await transport.GetAsync<StringResponse>("/my-index/_search?q=title:hello");
```

## Response Types

The generic type parameter on `Get<TResponse>`, `Post<TResponse>`, etc. controls how the response body is read:

| Type | Body Representation | Notes |
|---|---|---|
| `StringResponse` | `string` | Good for debugging and small payloads |
| `BytesResponse` | `byte[]` | Raw bytes, useful for binary content |
| `VoidResponse` | _(skipped)_ | Body is not read. Used for `HEAD` and fire-and-forget calls |
| `StreamResponse` | `Stream` | Caller **must** dispose. Best for large payloads |
| `DynamicResponse` | `DynamicDictionary` | Dynamic dictionary with typed path traversal |

## DynamicResponse

`DynamicResponse` deserializes JSON into a `DynamicDictionary` and exposes a `Get<T>()` method for safe, typed path traversal using dot-separated keys:

```csharp
var response = transport.Get<DynamicResponse>("/my-index/_search?q=title:hello");

// Traverse nested JSON with dot notation
int totalHits = response.Get<int>("hits.total.value");
string firstId = response.Get<string>("hits.hits.0._id");

// _arbitrary_key_ traverses into the first key at that level
string fieldType = response.Get<string>("my-index.mappings.properties._arbitrary_key_.type");
```

## Configuration

### Single node

```csharp
var settings = new TransportConfiguration(new Uri("http://localhost:9200"));
```

### Elastic Cloud (cloud ID)

```csharp
var settings = new TransportConfiguration("my-cloud-id", new ApiKey("base64key"));
// or
var settings = new TransportConfiguration("my-cloud-id", new BasicAuthentication("user", "pass"));
```

### Multiple nodes with a node pool

```csharp
var pool = new StaticNodePool(new[]
{
    new Node(new Uri("http://node1:9200")),
    new Node(new Uri("http://node2:9200")),
    new Node(new Uri("http://node3:9200"))
});
var settings = new TransportConfiguration(pool);
var transport = new DistributedTransport(settings);
```

### All components

```csharp
var pool = new StaticNodePool(new[] { new Node(new Uri("http://localhost:9200")) });
var requestInvoker = new HttpRequestInvoker();
var serializer = new LowLevelRequestResponseSerializer();
var product = ElasticsearchProductRegistration.Default;

var settings = new TransportConfiguration(pool, requestInvoker, serializer, product);
var transport = new DistributedTransport(settings);
```

## Request Pipeline

The transport models a request pipeline that handles node failover, sniffing, and pinging:

![Request Pipeline](https://raw.githubusercontent.com/elastic/elastic-transport-net/main/request-pipeline.png)

The pipeline introduces two special API calls:

- **Sniff** — queries the cluster to discover the current node topology
- **Ping** — the fastest possible request to check if a node is alive

The transport fails over in constant time. If a node is marked dead, it is skipped immediately (as long as the overall request timeout allows).

## Components

| Component | Description |
|---|---|
| `NodePool` | Registry of `Node` instances. Implementations: `SingleNodePool`, `StaticNodePool`, `SniffingNodePool`, `StickyNodePool`, `CloudNodePool` |
| `IRequestInvoker` | Abstraction for HTTP I/O. Default: `HttpRequestInvoker` |
| `Serializer` | Request/response serialization. Default uses `System.Text.Json` |
| `ProductRegistration` | Product-specific metadata, sniff/ping behavior. Use `ElasticsearchProductRegistration` for Elasticsearch |

## Observability

Every response inherits from `TransportResponse` and exposes an `ApiCallDetails` property:

```csharp
var response = transport.Get<StringResponse>("/");

// Structured call metadata
ApiCallDetails details = response.ApiCallDetails;
Console.WriteLine(details.HttpStatusCode);
Console.WriteLine(details.Uri);

// Human-readable debug string
Console.WriteLine(details.DebugInformation);
```

The transport also emits `DiagnosticSource` events for serialization timing, time-to-first-byte, and other counters.

## Links

- [GitHub Repository](https://github.com/elastic/elastic-transport-net)
- [Issues](https://github.com/elastic/elastic-transport-net/issues)
- [License (Apache 2.0)](https://github.com/elastic/elastic-transport-net/blob/main/LICENSE)
