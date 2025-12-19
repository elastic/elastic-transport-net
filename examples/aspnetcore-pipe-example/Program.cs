// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Text.Json;
using Elastic.Transport;
using HttpMethod = Elastic.Transport.HttpMethod;

var builder = WebApplication.CreateBuilder(args);

// Configure the Elastic Transport
// In a real app, you'd configure this with your Elasticsearch connection details
builder.Services.AddSingleton<ITransport>(_ =>
{
	var settings = new TransportConfiguration(new Uri("http://localhost:9200"));
	var transport = new DistributedTransport(settings);
	return transport;
});

var app = builder.Build();

// =============================================================================
// EXAMPLE 1: Forward request body using PipeReader
// Uses HttpContext.Request.BodyReader directly - zero-copy forwarding
// =============================================================================
app.MapPost("/forward-document/{index}", async (
	HttpContext context,
	ITransport transport,
	string index
) =>
{
	// Forward the incoming request body directly to Elasticsearch
	// using the PipeReader - no intermediate buffering needed
	var postData = PostData.PipeReader(context.Request.BodyReader);

	var response = await transport.RequestAsync<StringResponse>(
		HttpMethod.POST,
		$"/{index}/_doc",
		postData,
		cancellationToken: context.RequestAborted);

	if (!response.ApiCallDetails.HasSuccessfulStatusCode)
		context.Response.StatusCode = response.ApiCallDetails.HttpStatusCode ?? 500;

	return response.Body;
});

// =============================================================================
// EXAMPLE 2: Forward response body using PipeWriter
// Uses HttpContext.Response.BodyWriter directly - zero-copy streaming
// =============================================================================
app.MapGet("/search/{index}", async (
	HttpContext context,
	ITransport transport,
	string index,
	string? q
) =>
{
	var path = string.IsNullOrEmpty(q)
		? $"/{index}/_search"
		: $"/{index}/_search?q={Uri.EscapeDataString(q)}";

	// Get the response as a PipeResponse so we can stream directly
	await using var response = await transport.RequestAsync<PipeResponse>(
		HttpMethod.GET,
		path,
		cancellationToken: context.RequestAborted);

	// Set the response content type and status
	context.Response.ContentType = response.ContentType;
	if (!response.ApiCallDetails.HasSuccessfulStatusCode) context.Response.StatusCode = response.ApiCallDetails.HttpStatusCode ?? 500;

	// Stream the response directly to the client using PipeWriter
	// No intermediate buffering - data flows directly from Elasticsearch to client
	await response.CopyToAsync(context.Response.BodyWriter, context.RequestAborted);
});

// =============================================================================
// EXAMPLE 3: Full proxy - PipeReader in, PipeWriter out
// Complete zero-copy proxy for bulk operations
// =============================================================================
app.MapPost("/bulk/{index}", async (HttpContext context, ITransport transport, string index) =>
{
	// Forward the bulk request body using PipeReader
	var postData = PostData.PipeReader(context.Request.BodyReader);

	// Get response as PipeResponse for streaming output
	await using var response = await transport.RequestAsync<PipeResponse>(
		HttpMethod.POST,
		$"/{index}/_bulk",
		postData,
		cancellationToken: context.RequestAborted);

	context.Response.ContentType = response.ContentType;
	if (!response.ApiCallDetails.HasSuccessfulStatusCode) context.Response.StatusCode = response.ApiCallDetails.HttpStatusCode ?? 500;

	// Stream the response directly back to the client
	await response.CopyToAsync(context.Response.BodyWriter, context.RequestAborted);
});

// =============================================================================
// EXAMPLE 4: Using PipeWriter callback for serialization
// When you need to serialize an object efficiently using .NET 10 PipeWriter support
// =============================================================================
app.MapPost("/index-typed/{index}", async (
	HttpContext context,
	ITransport transport,
	string index
) =>
{
	// Read the incoming document
	var document = await JsonSerializer.DeserializeAsync<JsonDocument>(
		context.Request.Body,
		cancellationToken: context.RequestAborted);

	if (document is null)
	{
		context.Response.StatusCode = 400;
		return "Invalid JSON document";
	}

	// Use PipeWriter callback to serialize directly to the output pipe
	// This uses .NET 10's JsonSerializer.SerializeAsync(PipeWriter, ...) support
	var postData = PostData.PipeWriter(document, static async (doc, writer, ct) =>
	{
		await JsonSerializer.SerializeAsync(writer, doc, cancellationToken: ct);
	});

	var response = await transport.RequestAsync<StringResponse>(
		HttpMethod.POST,
		$"/{index}/_doc",
		postData,
		cancellationToken: context.RequestAborted);

	if (!response.ApiCallDetails.HasSuccessfulStatusCode) context.Response.StatusCode = response.ApiCallDetails.HttpStatusCode ?? 500;

	return response.Body;
});

// =============================================================================
// EXAMPLE 5: Reading response with PipeReader for deserialization
// Use .NET 10's JsonSerializer.DeserializeAsync(PipeReader, ...) support
// =============================================================================
app.MapGet("/get-document/{index}/{id}", async (
	HttpContext context,
	ITransport transport,
	string index,
	string id
) =>
{
	await using var response = await transport.RequestAsync<PipeResponse>(
		HttpMethod.GET,
		$"/{index}/_doc/{id}",
		cancellationToken: context.RequestAborted);

	if (!response.ApiCallDetails.HasSuccessfulStatusCode)
	{
		context.Response.StatusCode = response.ApiCallDetails.HttpStatusCode ?? 500;
		return Results.Problem($"Failed to get document: {response.ApiCallDetails.HttpStatusCode}");
	}

	// Use .NET 10's PipeReader deserialization support
	var document = await JsonSerializer.DeserializeAsync<JsonDocument>(
		response.Body,
		cancellationToken: context.RequestAborted);

	return Results.Json(document);
});

// =============================================================================
// EXAMPLE 6: Streaming search results with IAsyncEnumerable
// Use .NET 10's DeserializeAsyncEnumerable for streaming JSON arrays
// =============================================================================
app.MapGet("/stream-hits/{index}", async (
	HttpContext context,
	ITransport transport,
	string index
) =>
{
	var postData = PostData.Serializable(new
	{
		query = new { match_all = new { } },
		size = 1000
	});

	await using var response = await transport.RequestAsync<PipeResponse>(
		HttpMethod.POST,
		$"/{index}/_search",
		postData,
		cancellationToken: context.RequestAborted);

	if (!response.ApiCallDetails.HasSuccessfulStatusCode)
	{
		context.Response.StatusCode = response.ApiCallDetails.HttpStatusCode ?? 500;
		return;
	}

	context.Response.ContentType = "application/x-ndjson";

	// Note: For true streaming of hits, you'd need to parse the response structure.
	// This example shows the concept - in practice you'd use a scroll or PIT query
	// and stream results as they come in.

	// For demonstration, we stream the entire response
	await response.CopyToAsync(context.Response.BodyWriter, context.RequestAborted);
});

app.MapGet("/", () => """
					  ASP.NET Core + Elastic.Transport Pipe Example

					  This example demonstrates using .NET 10's PipeReader/PipeWriter support
					  with Elastic.Transport for zero-copy request/response streaming.

					  Endpoints:
					  - POST /forward-document/{index} - Forward request body to ES using PipeReader
					  - GET  /search/{index}?q=...     - Search and stream response using PipeWriter
					  - POST /bulk/{index}             - Full proxy: PipeReader in, PipeWriter out
					  - POST /index-typed/{index}      - Serialize using PipeWriter callback
					  - GET  /get-document/{index}/{id} - Deserialize using PipeReader
					  - GET  /stream-hits/{index}      - Stream search results

					  Requires Elasticsearch running at http://localhost:9200
					  """);

app.Run();
