# Elastic.Transport

Transport classes and utilities shared among .NET Elastic client libraries.

This library was lifted from [elasticsearch-net](https://github.com/elastic/elasticsearch-net) and then transformed to be used across all Elastic services rather than only Elasticsearch.

Provides the client's connectivity components, exposes a (potentially) cluster-aware request pipeline that can be resilient to nodes dropping in and out of rotation.
This package is heavily optimized for the Elastic (elastic.co) product suite and Elastic Cloud (cloud.elastic.co) SAAS offering.

The transport is designed to fail over fast and in constant time.
If a `Node` is considered bad it will fail over immediately given the overall request timeout allows for it.

It's an explicit non-goal to introduce full (incremental) retry mechanisms. This library is too generic to be making
these decisions and should be pushed on to the products/libraries making use of `Elastic.Transport`.


## Usage

This library can be used on its own but is typically used as the heart of a facade client that models all
the API endpoints.

In its most direct and terse form you can use the following to create requests:

```csharp
var settings = new TransportConfiguration(new Uri("http://localhost:9200"));
var transport = new DistributedTransport(settings);

var response = transport.Get<StringResponse>("/");
var headResponse = transport.Head("/");
```

`Get` and `Head` are extension methods to the only method `HttpTransport` dictates, namely `Request()` and its async variant.

Wrapping clients most likely will list out all `components` explicitly and use `DistributedTransport<TConfiguration>`:

```csharp
var pool = new StaticNodePool(new[] { new Node(new Uri("http://localhost:9200")) });
var requestInvoker = new HttpRequestInvoker();
var product = ElasticsearchProductRegistration.Default;

var settings = new TransportConfiguration(pool, requestInvoker, productRegistration: product);
var transport = new DistributedTransport(settings);

var response = transport.Request<StringResponse>(HttpMethod.GET, "/");
```

This allows implementers to extend `TransportConfiguration` with product/service specific configuration.


## Response Types

The generic type parameter on `Get<TResponse>`, `Post<TResponse>`, etc. controls how the response body is consumed:

| Type | Body | Notes |
|---|---|---|
| `StringResponse` | `string` | Good for debugging and small payloads |
| `BytesResponse` | `byte[]` | Raw bytes, useful for binary content |
| `StreamResponse` | `Stream` | Caller **must** dispose. Best for large payloads |
| `DynamicResponse` | `DynamicDictionary` | Dynamic path traversal via `Get<T>()` |
| `JsonResponse` | `JsonNode` | `System.Text.Json` DOM with safe `Get<T>()` path traversal |
| `VoidResponse` | _(skipped)_ | Body is not read. Used for `HEAD` and fire-and-forget calls |
| `PipeResponse` | `PipeReader` | .NET 10+ only. Zero-copy streaming via `System.IO.Pipelines` |

Products and custom clients can register additional response types by implementing `TypedResponseBuilder<TResponse>`.

See [docs/pipe-streaming.md](docs/pipe-streaming.md) for detailed `PipeResponse` and `PostData.Pipe*` documentation.


### Components

`HttpTransport` itself only defines `Request()` and `RequestAsync()` and all wrapping clients accept an `HttpTransport`.

The `HttpTransport` implementation that this library ships models a request pipeline that can deal with a large variety of topologies:

![request-pipeline.png](request-pipeline.png)

Whilst complex, every effort is made to only walk paths if we are certain they provide value.

It introduces two special API calls:

* **sniff** — a request to the service/product that should inform the client about the active topology.
* **ping** — the fastest request the transport can do to validate a `Node` is alive.

If you instantiate `DistributedTransport` you can pass an instance of `IProductRegistration` to provide implementations
for `sniff` and `ping`.

```csharp
var settings = new TransportConfiguration(new Uri("http://localhost:9200"));
var transport = new DistributedTransport(settings);
```

Will use the `DefaultProductRegistration` which opts out of `sniff` and `ping`.

However this library ships with a default implementation to fill in the blanks for Elasticsearch,
so we can create a transport for Elasticsearch that supports `sniff` and `ping` as follows:

```csharp
var uri = new Uri("http://localhost:9200");
var settings = new TransportConfiguration(uri, ElasticsearchProductRegistration.Default);
var transport = new DistributedTransport(settings);
```

### Injection

All components are optional and ship with sane defaults. Typically client users only provide
the `NodePool` to the transport configuration.

##### TransportConfiguration:

* `NodePool` — a registry of `Node` instances the transport will ask for a view it can iterate over.
  Only if a node pool indicates it supports receiving new nodes will the transport sniff.
* `IRequestInvoker` — abstraction for the actual I/O the transport needs to perform.
* `Serializer` — allows you to inject your own serializer; the default uses `System.Text.Json`.
* `IProductRegistration` — product-specific implementations and metadata provider.


#### Transport

* `ITransportConfiguration` — a transport configuration instance, explicitly designed for clients to introduce subclasses of.
* `RequestPipelineFactory` — a factory creating `RequestPipeline` instances.
* `DateTimeProvider` — abstraction around `DateTime.Now` so we can test algorithms without waiting on the clock.
* `MemoryStreamFactory` — a factory creating `MemoryStream` instances.



### Observability

The default `HttpTransport` implementation ships with various `DiagnosticSources` to make the whole
flow through the request pipeline auditable and debuggable.

Every response returned by the transport implements `TransportResponse` which exposes `ApiCallDetails`
holding all information relevant to the request and response.

`NOTE:` `response.ApiCallDetails.DebugInformation` always holds a human-readable string to indicate
what happened.

`DiagnosticSources` exist for various purposes, e.g. (de)serialization times, time to first byte and various counters.

## Mocking response objects for testing

`TestableResponseFactory` can be used to create response objects for use in unit tests.

Example code using the [Moq](https://github.com/moq/moq) library:

```csharp
var response = TestableResponseFactory.CreateSuccessfulResponse<SearchResponse<Document>>(new(), 200);
var mock = new Mock<ElasticsearchClient>();
mock
  .Setup(m => m.SearchAsync<Document>(It.IsAny<SearchRequest>(), It.IsAny<CancellationToken>()))
  .ReturnsAsync(response);
```
