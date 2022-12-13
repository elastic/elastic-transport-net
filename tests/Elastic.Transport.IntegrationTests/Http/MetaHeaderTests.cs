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

/// <summary>
/// Tests that the test framework loads a controller and the exposed transport can talk to its endpoints.
/// Tests runs against a server that started up once and its server instance shared among many test classes
/// </summary>
public class MetaHeaderTests : AssemblyServerTestsBase
{
	public MetaHeaderTests(TransportTestServer instance) : base(instance) { }

	[Fact]
	public async Task AddsExpectedMetaHeader()
	{
		var connection = new TestableHttpConnection(responseMessage =>
		{
			responseMessage.RequestMessage.Content.Headers.ContentType.ToString().Should().Be("application/vnd.elasticsearch+json;compatible-with=8");

			var acceptValues = responseMessage.RequestMessage.Headers.GetValues("Accept");
			acceptValues.Single().Should().Be("application/vnd.elasticsearch+json;compatible-with=8");

			var contentTypeValues = responseMessage.RequestMessage.Content.Headers.GetValues("Content-Type");
			contentTypeValues.Single().Should().Be("application/vnd.elasticsearch+json;compatible-with=8");
		});

		var connectionPool = new SingleNodePool(Server.Uri);
		var config = new TransportConfiguration(connectionPool, connection, productRegistration: new ElasticsearchProductRegistration(typeof(Clients.Elasticsearch.ElasticsearchClient)));
		var transport = new DefaultHttpTransport(config);

		var response = await transport.PostAsync<StringResponse>("/dummy/20", PostData.String("{}"));
	}
}

[ApiController, Route("[controller]")]
public class DummyController : ControllerBase
{
	[HttpGet("{id}")]
	public async Task<int> Get(int id) => await Task.FromResult(id * 3);
}
