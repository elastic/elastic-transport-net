// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Elastic.Transport.IntegrationTests.Plumbing;
using Elastic.Transport.IntegrationTests.Plumbing.Stubs;
using Elastic.Transport.Products.Elasticsearch;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Elastic.Transport.IntegrationTests.Http;

public class ApiCompatibilityHeaderTests(TestServerFixture instance) : AssemblyServerTestsBase(instance)
{
	private const string DefaultVendorMime = "application/vnd.elasticsearch+json;compatible-with=8";

	[Fact]
	public async Task AddsExpectedVendorInformationForRestApiCompaitbility()
	{
		var requestInvoker = new TrackingRequestInvoker(responseMessage =>
		{
			_ = responseMessage.RequestMessage.Content.Headers.ContentType.MediaType.Should().Be("application/vnd.elasticsearch+json");
			var parameter = responseMessage.RequestMessage.Content.Headers.ContentType.Parameters.Single();
			_ = parameter.Name.Should().Be("compatible-with");
			_ = parameter.Value.Should().Be("8");

			AssertHeader(responseMessage, "Accept", DefaultVendorMime);
			AssertContentTypeHeader(responseMessage, DefaultVendorMime);
		});

		await SendAsync(requestInvoker);
	}

	[Fact]
	public async Task OverridingAcceptWithVendorMimeAppendsCompatibleWith()
	{
		var requestInvoker = new TrackingRequestInvoker(responseMessage =>
		{
			AssertHeader(responseMessage, "Accept", DefaultVendorMime);
			AssertContentTypeHeader(responseMessage, DefaultVendorMime);
		});

		await SendAsync(requestInvoker, new RequestConfiguration { Accept = "application/vnd.elasticsearch+json" });
	}

	[Fact]
	public async Task OverridingAcceptWithExplicitCompatibleWithIsRespected()
	{
		var requestInvoker = new TrackingRequestInvoker(responseMessage =>
		{
			AssertHeader(responseMessage, "Accept", "application/vnd.elasticsearch+json;compatible-with=9");
			AssertContentTypeHeader(responseMessage, DefaultVendorMime);
		});

		await SendAsync(requestInvoker, new RequestConfiguration { Accept = "application/vnd.elasticsearch+json;compatible-with=9" });
	}

	[Fact]
	public async Task OverridingAcceptWithPlainJsonIsLeftUnchanged()
	{
		var requestInvoker = new TrackingRequestInvoker(responseMessage =>
		{
			AssertHeader(responseMessage, "Accept", "application/json");
			AssertContentTypeHeader(responseMessage, DefaultVendorMime);
		});

		await SendAsync(requestInvoker, new RequestConfiguration { Accept = "application/json" });
	}

	[Fact]
	public async Task OverridingAcceptWithTextPlainIsLeftUnchanged()
	{
		var requestInvoker = new TrackingRequestInvoker(responseMessage =>
		{
			AssertHeader(responseMessage, "Accept", "text/plain");
			AssertContentTypeHeader(responseMessage, DefaultVendorMime);
		});

		await SendAsync(requestInvoker, new RequestConfiguration { Accept = "text/plain" });
	}

	[Fact]
	public async Task OverridingContentTypeWithVendorMimeAppendsCompatibleWith()
	{
		var requestInvoker = new TrackingRequestInvoker(responseMessage =>
		{
			AssertHeader(responseMessage, "Accept", DefaultVendorMime);
			AssertContentTypeHeader(responseMessage, "application/vnd.elasticsearch+x-ndjson;compatible-with=8");
		});

		await SendAsync(requestInvoker, new RequestConfiguration { ContentType = "application/vnd.elasticsearch+x-ndjson" });
	}

	[Fact]
	public async Task WithoutClientMajorVersionDefaultsToBareVendorMime()
	{
		var requestInvoker = new TrackingRequestInvoker(responseMessage =>
		{
			AssertHeader(responseMessage, "Accept", "application/vnd.elasticsearch+json");
			AssertContentTypeHeader(responseMessage, "application/vnd.elasticsearch+json");
		});

		await SendWithDefaultRegistrationAsync(requestInvoker, requestConfiguration: null);
	}

	[Fact]
	public async Task WithoutClientMajorVersionHeaderOverridesAreNotTransformed()
	{
		var requestInvoker = new TrackingRequestInvoker(responseMessage =>
		{
			AssertHeader(responseMessage, "Accept", "application/vnd.elasticsearch+json");
			AssertContentTypeHeader(responseMessage, "application/vnd.elasticsearch+json");
		});

		await SendWithDefaultRegistrationAsync(
			requestInvoker,
			new RequestConfiguration { Accept = "application/vnd.elasticsearch+json", ContentType = "application/vnd.elasticsearch+json" });
	}

	private async Task SendWithDefaultRegistrationAsync(TrackingRequestInvoker requestInvoker, IRequestConfiguration requestConfiguration)
	{
		var nodePool = new SingleNodePool(Server.Uri);
		var config = new TransportConfiguration(nodePool, requestInvoker, productRegistration: ElasticsearchProductRegistration.Default);
		var transport = new DistributedTransport(config);

		_ = await transport.RequestAsync<StringResponse>(
			new EndpointPath(HttpMethod.POST, "/metaheader"),
			PostData.String("{}"),
			default,
			requestConfiguration,
			TestContext.Current.CancellationToken);
	}

	[Fact]
	public async Task MultiValueAcceptListAppendsCompatibleWithToEachVendorEntry()
	{
		var requestInvoker = new TrackingRequestInvoker(responseMessage =>
		{
			AssertHeader(
				responseMessage,
				"Accept",
				"application/vnd.elasticsearch+json;compatible-with=8,application/vnd.elasticsearch+x-ndjson;compatible-with=8");
			AssertContentTypeHeader(responseMessage, DefaultVendorMime);
		});

		await SendAsync(
			requestInvoker,
			new RequestConfiguration { Accept = "application/vnd.elasticsearch+json,application/vnd.elasticsearch+x-ndjson" });
	}

	private Task SendAsync(TrackingRequestInvoker requestInvoker) => SendAsync(requestInvoker, null);

	private async Task SendAsync(TrackingRequestInvoker requestInvoker, IRequestConfiguration requestConfiguration)
	{
		var nodePool = new SingleNodePool(Server.Uri);
		var config = new TransportConfiguration(nodePool, requestInvoker, productRegistration: new ElasticsearchProductRegistration(typeof(Clients.Elasticsearch.ElasticsearchClient)));
		var transport = new DistributedTransport(config);

		_ = await transport.RequestAsync<StringResponse>(
			new EndpointPath(HttpMethod.POST, "/metaheader"),
			PostData.String("{}"),
			default,
			requestConfiguration,
			TestContext.Current.CancellationToken);
	}

	private static void AssertHeader(System.Net.Http.HttpResponseMessage responseMessage, string headerName, string expected)
	{
		var values = responseMessage.RequestMessage.Headers.GetValues(headerName);
		_ = string.Join(",", values).Replace(" ", "").Should().Be(expected);
	}

	private static void AssertContentTypeHeader(System.Net.Http.HttpResponseMessage responseMessage, string expected)
	{
		var values = responseMessage.RequestMessage.Content.Headers.GetValues("Content-Type");
		_ = string.Join(",", values).Replace(" ", "").Should().Be(expected);
	}
}

[ApiController, Route("[controller]")]
public class MetaHeaderController : ControllerBase
{
	[HttpPost()]
	public async Task<int> Post() => await Task.FromResult(100);
}
