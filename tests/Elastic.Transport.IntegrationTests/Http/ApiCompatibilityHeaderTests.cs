// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Linq;
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
	[Fact]
	public async Task AddsExpectedVendorInformationForRestApiCompaitbility()
	{
		var requestInvoker = new TrackingRequestInvoker(responseMessage =>
		{
			_ = responseMessage.RequestMessage.Content.Headers.ContentType.MediaType.Should().Be("application/vnd.elasticsearch+json");
			var parameter = responseMessage.RequestMessage.Content.Headers.ContentType.Parameters.Single();
			_ = parameter.Name.Should().Be("compatible-with");
			_ = parameter.Value.Should().Be("8");

			var acceptValues = responseMessage.RequestMessage.Headers.GetValues("Accept");
			_ = acceptValues.Single().Replace(" ", "").Should().Be("application/vnd.elasticsearch+json;compatible-with=8");

			var contentTypeValues = responseMessage.RequestMessage.Content.Headers.GetValues("Content-Type");
			_ = contentTypeValues.Single().Replace(" ", "").Should().Be("application/vnd.elasticsearch+json;compatible-with=8");
		});

		var nodePool = new SingleNodePool(Server.Uri);
		var config = new TransportConfiguration(nodePool, requestInvoker, productRegistration: new ElasticsearchProductRegistration(typeof(Clients.Elasticsearch.ElasticsearchClient)));
		var transport = new DistributedTransport(config);

		var response = await transport.PostAsync<StringResponse>("/metaheader", PostData.String("{}"), cancellationToken: TestContext.Current.CancellationToken);
	}
}

[ApiController, Route("[controller]")]
public class MetaHeaderController : ControllerBase
{
	[HttpPost()]
	public async Task<int> Post() => await Task.FromResult(100);
}
