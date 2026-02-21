// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Buffers;
using System.IO.Pipelines;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Elastic.Transport.Pipes.IntegrationTests;

/// <summary>
/// A mock Elasticsearch server that provides endpoints for testing pipe-based request/response handling.
/// </summary>
public class MockElasticsearchServer : IAsyncLifetime, IDisposable
{
	private readonly IHost _host;
	private readonly IServer _server;

	public MockElasticsearchServer()
	{
		var url = "http://127.0.0.1:0";

		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?> { ["urls"] = url })
			.Build();

		_host = new HostBuilder()
			.ConfigureWebHost(builder =>
				builder.UseKestrel()
					.UseConfiguration(configuration)
					.UseStartup<MockElasticsearchStartup>()
			)
			.Build();

		_server = _host.Services.GetRequiredService<IServer>();
	}

	public Uri Uri { get; private set; } = null!;

	public ITransport Transport { get; private set; } = null!;

	public async ValueTask InitializeAsync()
	{
		await _host.StartAsync();
		var addresses = _server.Features.Get<IServerAddressesFeature>();
		var address = addresses?.Addresses.FirstOrDefault() ?? throw new InvalidOperationException("No server address found");
		Uri = new Uri(address);
		Transport = new DistributedTransport(new TransportConfiguration(Uri));
	}

	public async ValueTask DisposeAsync()
	{
		await _host.StopAsync();
		_host.Dispose();
		GC.SuppressFinalize(this);
	}

	public void Dispose()
	{
		_host.Dispose();
		GC.SuppressFinalize(this);
	}
}

public class MockElasticsearchStartup(IConfiguration configuration)
{
	public IConfiguration Configuration { get; } = configuration;

	private static readonly string[] ChunkedResponse =
	[
		"{\"status\":",
				"\"ok\",",
				"\"message\":",
				"\"Hello from chunked response\"}"
	];

	public void ConfigureServices(IServiceCollection services) => services.AddControllers();

	public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
	{
		app.UseRouting();
		app.UseEndpoints(MapEndpoints);
	}

	private static void MapEndpoints(IEndpointRouteBuilder endpoints)
	{
		// Echo endpoint - returns the request body as response
		endpoints.MapPost("/echo", async context =>
		{
			context.Response.ContentType = "application/json";

			// Buffer the entire request body first to avoid deadlocks with HTTP/1.1
			using var ms = new MemoryStream();
			await context.Request.Body.CopyToAsync(ms, context.RequestAborted);
			var requestBytes = ms.ToArray();

			// Write the buffered body to the response
			context.Response.ContentLength = requestBytes.Length;
			await context.Response.Body.WriteAsync(requestBytes, context.RequestAborted);
		});

		// Index document endpoint - simulates /{index}/_doc
		endpoints.MapPost("/{index}/_doc", async context =>
		{
			var index = context.Request.RouteValues["index"]?.ToString() ?? "unknown";

			// Read the request body
			using var ms = new MemoryStream();
			await context.Request.Body.CopyToAsync(ms, context.RequestAborted);
			var requestBody = Encoding.UTF8.GetString(ms.ToArray());

			// Create a mock Elasticsearch response
			var response = new
			{
				_index = index,
				_id = Guid.NewGuid().ToString("N")[..20],
				_version = 1,
				result = "created",
				_shards = new { total = 2, successful = 1, failed = 0 },
				_seq_no = 0,
				_primary_term = 1
			};

			context.Response.ContentType = "application/json";
			context.Response.StatusCode = 201;

			await JsonSerializer.SerializeAsync(context.Response.BodyWriter, response, cancellationToken: context.RequestAborted);
		});

		// Search endpoint - simulates /{index}/_search
		endpoints.MapMethods("/{index}/_search", ["GET", "POST"], async context =>
		{
			var index = context.Request.RouteValues["index"]?.ToString() ?? "unknown";
			var query = context.Request.Query["q"].FirstOrDefault();

			// Create a mock search response
			var response = new
			{
				took = 5,
				timed_out = false,
				_shards = new { total = 1, successful = 1, skipped = 0, failed = 0 },
				hits = new
				{
					total = new { value = 2, relation = "eq" },
					max_score = 1.0,
					hits = new[]
					{
						new
						{
							_index = index,
							_id = "1",
							_score = 1.0,
							_source = new { title = "Document 1", query }
						},
						new
						{
							_index = index,
							_id = "2",
							_score = 0.9,
							_source = new { title = "Document 2", query }
						}
					}
				}
			};

			context.Response.ContentType = "application/json";
			await JsonSerializer.SerializeAsync(context.Response.BodyWriter, response, cancellationToken: context.RequestAborted);
		});

		// Get document endpoint - simulates /{index}/_doc/{id}
		endpoints.MapGet("/{index}/_doc/{id}", async context =>
		{
			var index = context.Request.RouteValues["index"]?.ToString() ?? "unknown";
			var id = context.Request.RouteValues["id"]?.ToString() ?? "unknown";

			var response = new
			{
				_index = index,
				_id = id,
				_version = 1,
				_seq_no = 0,
				_primary_term = 1,
				found = true,
				_source = new { title = $"Document {id}", content = "Test content" }
			};

			context.Response.ContentType = "application/json";
			await JsonSerializer.SerializeAsync(context.Response.BodyWriter, response, cancellationToken: context.RequestAborted);
		});

		// Bulk endpoint - simulates /{index}/_bulk
		endpoints.MapPost("/{index}/_bulk", async context =>
		{
			var index = context.Request.RouteValues["index"]?.ToString() ?? "unknown";

			// Count lines (NDJSON format - each action is 2 lines)
			using var reader = new StreamReader(context.Request.Body);
			var content = await reader.ReadToEndAsync(context.RequestAborted);
			var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
			var itemCount = lines.Length / 2;

			// Create bulk response
			var items = Enumerable.Range(0, itemCount).Select(i => new
			{
				index = new
				{
					_index = index,
					_id = Guid.NewGuid().ToString("N")[..20],
					_version = 1,
					result = "created",
					_shards = new { total = 2, successful = 1, failed = 0 },
					status = 201,
					_seq_no = i,
					_primary_term = 1
				}
			}).ToArray();

			var response = new
			{
				took = 30,
				errors = false,
				items
			};

			context.Response.ContentType = "application/json";
			await JsonSerializer.SerializeAsync(context.Response.BodyWriter, response, cancellationToken: context.RequestAborted);
		});

		// Large response endpoint - for testing streaming
		endpoints.MapGet("/large-response", async context =>
		{
			context.Response.ContentType = "application/json";

			var writer = context.Response.BodyWriter;

			// Write a large JSON array in chunks
			await writer.WriteAsync("["u8.ToArray(), context.RequestAborted);

			for (var i = 0; i < 100; i++)
			{
				if (i > 0)
					await writer.WriteAsync(","u8.ToArray(), context.RequestAborted);

				var item = JsonSerializer.SerializeToUtf8Bytes(new
				{
					id = i,
					data = new string('x', 1000) // 1KB per item
				});
				await writer.WriteAsync(item, context.RequestAborted);
				await writer.FlushAsync(context.RequestAborted);
			}

			await writer.WriteAsync("]"u8.ToArray(), context.RequestAborted);
		});

		// Chunked response endpoint
		endpoints.MapGet("/chunked", async context =>
		{
			context.Response.ContentType = "application/json";

			var chunks = ChunkedResponse;

			foreach (var chunk in chunks)
			{
				await context.Response.BodyWriter.WriteAsync(Encoding.UTF8.GetBytes(chunk), context.RequestAborted);
				await context.Response.BodyWriter.FlushAsync(context.RequestAborted);
				await Task.Delay(10, context.RequestAborted); // Small delay between chunks
			}
		});
	}
}
