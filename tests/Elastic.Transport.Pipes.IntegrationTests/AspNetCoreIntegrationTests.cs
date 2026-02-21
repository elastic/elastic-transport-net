// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Elastic.Transport.Pipes.IntegrationTests;

/// <summary>
/// Integration tests that verify the ASP.NET Core example endpoints work correctly
/// by setting up both a mock Elasticsearch server and an ASP.NET Core test host.
/// </summary>
public class AspNetCoreIntegrationTests : IAsyncLifetime
{
	private IHost? _mockElasticsearch;
	private IHost? _aspNetCoreApp;
	private HttpClient? _client;
	private Uri? _elasticsearchUri;

	public async ValueTask InitializeAsync()
	{
		// Start mock Elasticsearch server
		_mockElasticsearch = await StartMockElasticsearchAsync();
		_elasticsearchUri = GetServerUri(_mockElasticsearch);

		// Start ASP.NET Core app pointing to mock Elasticsearch
		_aspNetCoreApp = await StartAspNetCoreAppAsync(_elasticsearchUri);
		var appUri = GetServerUri(_aspNetCoreApp);

		_client = new HttpClient { BaseAddress = appUri };
	}

	public async ValueTask DisposeAsync()
	{
		GC.SuppressFinalize(this);
		_client?.Dispose();

		if (_aspNetCoreApp is not null)
		{
			await _aspNetCoreApp.StopAsync();
			_aspNetCoreApp.Dispose();
		}

		if (_mockElasticsearch is not null)
		{
			await _mockElasticsearch.StopAsync();
			_mockElasticsearch.Dispose();
		}
	}

	[Fact]
	public async Task ForwardDocumentForwardsRequestUsingPipeReader()
	{
		// Arrange
		var document = new { title = "Test Document", author = "Test Author" };
		var content = new StringContent(
			JsonSerializer.Serialize(document),
			Encoding.UTF8,
			"application/json");

		// Act
		var response = await _client!.PostAsync("/forward-document/test-index", content);

		// Assert
		response.IsSuccessStatusCode.Should().BeTrue();

		var responseBody = await response.Content.ReadAsStringAsync();
		var responseDoc = JsonSerializer.Deserialize<JsonElement>(responseBody);

		responseDoc.GetProperty("_index").GetString().Should().Be("test-index");
		responseDoc.GetProperty("result").GetString().Should().Be("created");
	}

	[Fact]
	public async Task SearchStreamsResponseUsingPipeWriter()
	{
		// Act
		var response = await _client!.GetAsync("/search/my-index?q=hello");

		// Assert
		response.IsSuccessStatusCode.Should().BeTrue();
		response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

		var responseBody = await response.Content.ReadAsStringAsync();
		var searchResult = JsonSerializer.Deserialize<JsonElement>(responseBody);

		searchResult.GetProperty("took").GetInt32().Should().Be(5);
		searchResult.GetProperty("hits").GetProperty("total").GetProperty("value").GetInt32().Should().Be(2);
	}

	[Fact]
	public async Task BulkFullProxyWithPipeReaderAndPipeWriter()
	{
		// Arrange - NDJSON bulk format
		var bulkBody = new StringBuilder();
		bulkBody.AppendLine(JsonSerializer.Serialize(new { index = new { _index = "test" } }));
		bulkBody.AppendLine(JsonSerializer.Serialize(new { title = "Doc 1" }));
		bulkBody.AppendLine(JsonSerializer.Serialize(new { index = new { _index = "test" } }));
		bulkBody.AppendLine(JsonSerializer.Serialize(new { title = "Doc 2" }));

		var content = new StringContent(bulkBody.ToString(), Encoding.UTF8, "application/x-ndjson");

		// Act
		var response = await _client!.PostAsync("/bulk/test-index", content);

		// Assert
		response.IsSuccessStatusCode.Should().BeTrue();

		var responseBody = await response.Content.ReadAsStringAsync();
		var bulkResult = JsonSerializer.Deserialize<JsonElement>(responseBody);

		bulkResult.GetProperty("errors").GetBoolean().Should().BeFalse();
		bulkResult.GetProperty("items").GetArrayLength().Should().Be(2);
	}

	[Fact]
	public async Task GetDocumentDeserializesUsingPipeReader()
	{
		// Act
		var response = await _client!.GetAsync("/get-document/test-index/doc-123");

		// Assert
		response.IsSuccessStatusCode.Should().BeTrue();

		var responseBody = await response.Content.ReadAsStringAsync();
		var document = JsonSerializer.Deserialize<JsonElement>(responseBody);

		document.GetProperty("_index").GetString().Should().Be("test-index");
		document.GetProperty("_id").GetString().Should().Be("doc-123");
	}

	[Fact]
	public async Task IndexTypedSerializesUsingPipeWriterCallback()
	{
		// Arrange
		var document = new { title = "Typed Document", value = 42 };
		var content = new StringContent(
			JsonSerializer.Serialize(document),
			Encoding.UTF8,
			"application/json");

		// Act
		var response = await _client!.PostAsync("/index-typed/typed-index", content);

		// Assert
		response.IsSuccessStatusCode.Should().BeTrue();

		var responseBody = await response.Content.ReadAsStringAsync();
		var responseDoc = JsonSerializer.Deserialize<JsonElement>(responseBody);

		responseDoc.GetProperty("_index").GetString().Should().Be("typed-index");
		responseDoc.GetProperty("result").GetString().Should().Be("created");
	}

	private static async Task<IHost> StartMockElasticsearchAsync()
	{
		var host = Host.CreateDefaultBuilder()
			.ConfigureWebHostDefaults(webBuilder =>
			{
				webBuilder.UseKestrel(options => options.Listen(IPAddress.Loopback, 0));
				webBuilder.UseStartup<MockElasticsearchStartup>();
			})
			.Build();

		await host.StartAsync();
		return host;
	}

	private static async Task<IHost> StartAspNetCoreAppAsync(Uri elasticsearchUri)
	{
		var host = Host.CreateDefaultBuilder()
			.ConfigureWebHostDefaults(webBuilder =>
			{
				webBuilder.UseKestrel(options => options.Listen(IPAddress.Loopback, 0));
				webBuilder.ConfigureServices(services =>
				{
					// Register transport pointing to mock Elasticsearch
					// Disable compression for clearer test results
					var settings = new TransportConfiguration(elasticsearchUri)
					{
						EnableHttpCompression = false
					};
					var transport = new DistributedTransport(settings);
					services.AddSingleton<ITransport>(transport);
				});
				webBuilder.Configure(ConfigureExampleApp);
			})
			.Build();

		await host.StartAsync();
		return host;
	}

	private static void ConfigureExampleApp(IApplicationBuilder app)
	{
		app.UseRouting();
		app.UseEndpoints(endpoints =>
		{
			// Forward document endpoint
			endpoints.MapPost("/forward-document/{index}", async context =>
			{
				var transport = context.RequestServices.GetRequiredService<ITransport>();
				var index = context.Request.RouteValues["index"]?.ToString();

				var postData = PostData.PipeReader(context.Request.BodyReader);
				var response = await transport.RequestAsync<StringResponse>(
					HttpMethod.POST,
					$"/{index}/_doc",
					postData,
					cancellationToken: context.RequestAborted);

				if (!response.ApiCallDetails.HasSuccessfulStatusCode)
					context.Response.StatusCode = response.ApiCallDetails.HttpStatusCode ?? 500;

				await context.Response.WriteAsync(response.Body ?? "");
			});

			// Search endpoint
			endpoints.MapGet("/search/{index}", async context =>
			{
				var transport = context.RequestServices.GetRequiredService<ITransport>();
				var index = context.Request.RouteValues["index"]?.ToString();
				var q = context.Request.Query["q"].FirstOrDefault();

				var path = string.IsNullOrEmpty(q)
					? $"/{index}/_search"
					: $"/{index}/_search?q={Uri.EscapeDataString(q)}";

				await using var response = await transport.RequestAsync<PipeResponse>(
					HttpMethod.GET,
					path,
					cancellationToken: context.RequestAborted);

				context.Response.ContentType = response.ContentType;
				if (!response.ApiCallDetails.HasSuccessfulStatusCode)
					context.Response.StatusCode = response.ApiCallDetails.HttpStatusCode ?? 500;

				await response.CopyToAsync(context.Response.BodyWriter, context.RequestAborted);
			});

			// Bulk endpoint
			endpoints.MapPost("/bulk/{index}", async context =>
			{
				var transport = context.RequestServices.GetRequiredService<ITransport>();
				var index = context.Request.RouteValues["index"]?.ToString();

				var postData = PostData.PipeReader(context.Request.BodyReader);

				await using var response = await transport.RequestAsync<PipeResponse>(
					HttpMethod.POST,
					$"/{index}/_bulk",
					postData,
					cancellationToken: context.RequestAborted);

				context.Response.ContentType = response.ContentType;
				if (!response.ApiCallDetails.HasSuccessfulStatusCode)
					context.Response.StatusCode = response.ApiCallDetails.HttpStatusCode ?? 500;

				await response.CopyToAsync(context.Response.BodyWriter, context.RequestAborted);
			});

			// Get document endpoint
			endpoints.MapGet("/get-document/{index}/{id}", async context =>
			{
				var transport = context.RequestServices.GetRequiredService<ITransport>();
				var index = context.Request.RouteValues["index"]?.ToString();
				var id = context.Request.RouteValues["id"]?.ToString();

				await using var response = await transport.RequestAsync<PipeResponse>(
					HttpMethod.GET,
					$"/{index}/_doc/{id}",
					cancellationToken: context.RequestAborted);

				if (!response.ApiCallDetails.HasSuccessfulStatusCode)
				{
					context.Response.StatusCode = response.ApiCallDetails.HttpStatusCode ?? 500;
					return;
				}

				context.Response.ContentType = "application/json";
				await response.CopyToAsync(context.Response.BodyWriter, context.RequestAborted);
			});

			// Index typed endpoint
			endpoints.MapPost("/index-typed/{index}", async context =>
			{
				var transport = context.RequestServices.GetRequiredService<ITransport>();
				var index = context.Request.RouteValues["index"]?.ToString();

				var document = await JsonSerializer.DeserializeAsync<JsonDocument>(
					context.Request.Body,
					cancellationToken: context.RequestAborted);

				if (document is null)
				{
					context.Response.StatusCode = 400;
					await context.Response.WriteAsync("Invalid JSON");
					return;
				}

				var postData = PostData.PipeWriter(document, static async (doc, writer, ct) =>
				{
					await JsonSerializer.SerializeAsync(writer, doc, cancellationToken: ct);
				});

				var response = await transport.RequestAsync<StringResponse>(
					HttpMethod.POST,
					$"/{index}/_doc",
					postData,
					cancellationToken: context.RequestAborted);

				if (!response.ApiCallDetails.HasSuccessfulStatusCode)
					context.Response.StatusCode = response.ApiCallDetails.HttpStatusCode ?? 500;

				await context.Response.WriteAsync(response.Body ?? "");
			});
		});
	}

	private static Uri GetServerUri(IHost host)
	{
		var server = host.Services.GetRequiredService<IServer>();
		var addresses = server.Features.Get<IServerAddressesFeature>();
		var address = addresses?.Addresses.FirstOrDefault()
			?? throw new InvalidOperationException("No server address found");
		return new Uri(address);
	}
}
