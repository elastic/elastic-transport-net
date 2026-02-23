# ASP.NET Core PipeReader/PipeWriter Example

This example demonstrates how to use .NET 10's `System.IO.Pipelines` integration with `Elastic.Transport` for high-performance, zero-copy request/response streaming in ASP.NET Core applications.

## Requirements

- .NET 10 SDK
- Elasticsearch running at `http://localhost:9200`

## Key Concepts

### PipeReader for Request Bodies

ASP.NET Core exposes `HttpContext.Request.BodyReader` as a `PipeReader`. You can forward this directly to Elasticsearch without intermediate buffering:

```csharp
app.MapPost("/forward/{index}", async (HttpContext context, ITransport transport, string index) =>
{
    // Zero-copy forwarding of request body
    var postData = PostData.PipeReader(context.Request.BodyReader);
    var response = await transport.RequestAsync<StringResponse>(
        HttpMethod.POST, $"/{index}/_doc", postData);
    return response.Body;
});
```

### PipeWriter for Response Bodies

ASP.NET Core exposes `HttpContext.Response.BodyWriter` as a `PipeWriter`. You can stream Elasticsearch responses directly to the client:

```csharp
app.MapGet("/search/{index}", async (HttpContext context, ITransport transport, string index) =>
{
    await using var response = await transport.RequestAsync<PipeResponse>(
        HttpMethod.GET, $"/{index}/_search");

    context.Response.ContentType = response.ContentType;

    // Zero-copy streaming to client
    await response.CopyToAsync(context.Response.BodyWriter, context.RequestAborted);
});
```

### Full Proxy Pattern

Combine both for complete zero-copy proxying:

```csharp
app.MapPost("/bulk/{index}", async (HttpContext context, ITransport transport, string index) =>
{
    // Read from client's PipeReader
    var postData = PostData.PipeReader(context.Request.BodyReader);

    // Get response as PipeResponse
    await using var response = await transport.RequestAsync<PipeResponse>(
        HttpMethod.POST, $"/{index}/_bulk", postData);

    context.Response.ContentType = response.ContentType;

    // Write to client's PipeWriter
    await response.CopyToAsync(context.Response.BodyWriter, context.RequestAborted);
});
```

## API Reference

### PostData.PipeReader(PipeReader)

Creates a `PostData` instance that reads from an existing `PipeReader`. Ideal for forwarding ASP.NET Core request bodies.

### PostData.PipeWriter<T>(T state, Func<T, PipeWriter, CancellationToken, Task>)

Creates a `PostData` instance that invokes your callback with a `PipeWriter`. Ideal for efficient serialization using .NET 10's `JsonSerializer.SerializeAsync(PipeWriter, ...)`.

### PipeResponse

A `TransportResponse` that exposes the response body as a `PipeReader`:

- `Body` - The `PipeReader` for direct deserialization
- `ContentType` - The response MIME type
- `CopyToAsync(PipeWriter)` - Efficiently copy to another `PipeWriter`

## Running the Example

```bash
# Start Elasticsearch (e.g., using Docker)
docker run -d -p 9200:9200 -e "discovery.type=single-node" elasticsearch:8.x

# Run the example
dotnet run

# Test endpoints
curl http://localhost:5000/
curl -X POST http://localhost:5000/forward-document/test-index -H "Content-Type: application/json" -d '{"title":"Hello"}'
curl http://localhost:5000/search/test-index?q=title:Hello
```

## Performance Benefits

Using `PipeReader`/`PipeWriter` directly provides:

1. **Zero-copy streaming** - Data flows directly between ASP.NET Core and Elasticsearch without intermediate byte array allocations
2. **Backpressure support** - Built-in flow control prevents memory exhaustion with large payloads
3. **Reduced GC pressure** - Buffers are pooled and reused via `ArrayPool<byte>`
4. **Lower latency** - No need to buffer entire request/response before forwarding
