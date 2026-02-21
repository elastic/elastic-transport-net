# Zero-copy streaming with `System.IO.Pipelines`

> **Requires .NET 10 or later.**

`Elastic.Transport` integrates with [`System.IO.Pipelines`](https://learn.microsoft.com/dotnet/standard/io/pipelines) to provide zero-copy streaming for both request and response bodies. This avoids intermediate `byte[]` and `MemoryStream` allocations that are otherwise needed when proxying or processing large payloads.

## Overview

| Type | Direction | Purpose |
|---|---|---|
| `PipeResponse` | Response | Exposes the HTTP response body as a `PipeReader` |
| `PostData.PipeReader(PipeReader)` | Request | Forwards an existing `PipeReader` as the request body |
| `PostData.PipeWriter<T>(T, Func)` | Request | Serializes an object directly to a `PipeWriter` |

All three are registered automatically when targeting `net10.0` — no additional configuration is required.

## `PipeResponse`

Request a `PipeResponse` to receive the response body as a [`PipeReader`](https://learn.microsoft.com/dotnet/api/system.io.pipelines.pipereader):

```csharp
await using var response = await transport.GetAsync<PipeResponse>("/my-index/_search");
```

### Properties

| Property | Type | Description |
|---|---|---|
| `Body` | `PipeReader` | The response body. Supports `JsonSerializer.DeserializeAsync(PipeReader, ...)` on .NET 10. |
| `ContentType` | `string` | The MIME type of the response (e.g. `application/json`). |
| `ApiCallDetails` | `ApiCallDetails` | Standard transport call metadata (status code, URI, timing, etc.). |

### Deserializing from `PipeReader`

.NET 10 adds `JsonSerializer` overloads that accept `PipeReader` directly:

```csharp
await using var response = await transport.GetAsync<PipeResponse>("/my-index/_doc/1");

if (response.ApiCallDetails.HasSuccessfulStatusCode)
{
    var doc = await JsonSerializer.DeserializeAsync<MyDocument>(response.Body);
}
```

For streaming collections use `DeserializeAsyncEnumerable`:

```csharp
await using var response = await transport.PostAsync<PipeResponse>("/my-index/_search", postData);

await foreach (var hit in JsonSerializer.DeserializeAsyncEnumerable<Hit>(response.Body))
{
    // Process each hit as it arrives
}
```

### Forwarding responses with `CopyToAsync`

`CopyToAsync` copies the response body directly to a `PipeWriter` without intermediate buffering. This is the fastest way to forward an Elasticsearch response to an ASP.NET Core client:

```csharp
app.MapGet("/search/{index}", async (string index, HttpContext context, DistributedTransport transport) =>
{
    var path = $"/{index}/_search";
    await using var response = await transport.GetAsync<PipeResponse>(path, cancellationToken: context.RequestAborted);

    context.Response.ContentType = response.ContentType;
    context.Response.StatusCode = response.ApiCallDetails.HttpStatusCode ?? 502;
    await response.CopyToAsync(context.Response.BodyWriter, context.RequestAborted);
});
```

### Disposal

`PipeResponse` implements both `IAsyncDisposable` and `IDisposable`. It **must** be disposed to complete the `PipeReader` and release the underlying HTTP connection back to the pool. Always use `await using`:

```csharp
await using var response = await transport.GetAsync<PipeResponse>(path);
// Use response.Body here...
// Connection is returned to the pool when the using block exits.
```

## `PostData.PipeReader`

Wraps an existing `PipeReader` as a request body. The transport reads from the pipe and writes directly to the HTTP request stream.

```csharp
public static PostData PostData.PipeReader(PipeReader pipeReader);
```

This is ideal for forwarding an incoming ASP.NET Core request body to Elasticsearch without any intermediate buffering:

```csharp
app.MapPost("/forward/{index}", async (string index, HttpContext context, DistributedTransport transport) =>
{
    var postData = PostData.PipeReader(context.Request.BodyReader);
    var response = await transport.PostAsync<StringResponse>(
        $"/{index}/_doc", postData, cancellationToken: context.RequestAborted);

    context.Response.StatusCode = response.ApiCallDetails.HttpStatusCode ?? 502;
    return Results.Text(response.Body, response.ApiCallDetails.MimeType);
});
```

When `DisableDirectStreaming` is enabled on the transport configuration the data is buffered into memory first and the captured bytes are available via `ApiCallDetails.RequestBodyInBytes`.

## `PostData.PipeWriter<T>`

Accepts a state object and an async callback. The callback receives a `PipeWriter` that the transport feeds into the HTTP request stream:

```csharp
public static PostData PostData.PipeWriter<T>(
    T state,
    Func<T, PipeWriter, CancellationToken, Task> asyncWriter);
```

This lets you serialize objects directly to the wire using .NET 10's `JsonSerializer.SerializeAsync(PipeWriter, ...)`:

```csharp
var document = new MyDocument { Title = "Hello" };

var postData = PostData.PipeWriter(document, async (doc, writer, ct) =>
{
    await JsonSerializer.SerializeAsync(writer, doc, cancellationToken: ct);
});

var response = await transport.PostAsync<StringResponse>("/my-index/_doc", postData);
```

The state parameter avoids closure allocations — pass the data you need directly instead of capturing variables.

## Full proxy example

Combining `PostData.PipeReader` for the request and `PipeResponse` for the response gives you a zero-copy proxy with backpressure:

```csharp
app.MapPost("/proxy/{**path}", async (string path, HttpContext context, DistributedTransport transport) =>
{
    var postData = PostData.PipeReader(context.Request.BodyReader);
    await using var response = await transport.PostAsync<PipeResponse>(
        $"/{path}", postData, cancellationToken: context.RequestAborted);

    context.Response.ContentType = response.ContentType;
    context.Response.StatusCode = response.ApiCallDetails.HttpStatusCode ?? 502;
    await response.CopyToAsync(context.Response.BodyWriter, context.RequestAborted);
});
```

## When to use pipes vs. other response types

| Response type | Best for |
|---|---|
| `StringResponse` | Small responses where you need the raw string |
| `BytesResponse` | Small responses where you need raw bytes |
| `DynamicResponse` | Exploratory queries, dynamic field access |
| `JsonResponse` | DOM-level access via `JsonNode` without a POCO |
| `PipeResponse` | Large payloads, proxying, streaming, or when you want to deserialize directly from `PipeReader` |
| Custom `TransportResponse` | Typed deserialization via a registered `IResponseBuilder` |

## Performance characteristics

- **Zero-copy**: Data flows from the network socket through kernel buffers into your `PipeReader`/`PipeWriter` without extra managed allocations.
- **Backpressure**: `System.IO.Pipelines` applies backpressure automatically — a slow consumer won't cause unbounded memory growth.
- **Reduced GC pressure**: Avoids `byte[]` and `MemoryStream` allocations that would otherwise land on the large object heap for big payloads.
- **Chunked transfer encoding**: Works transparently with HTTP chunked responses.

## Further reading

- [Complete ASP.NET Core example](../examples/aspnetcore-pipe-example) with six endpoint patterns
- [System.IO.Pipelines documentation](https://learn.microsoft.com/dotnet/standard/io/pipelines)
- [JsonSerializer PipeReader/PipeWriter support in .NET 10](https://learn.microsoft.com/dotnet/standard/serialization/system-text-json/deserialization)
