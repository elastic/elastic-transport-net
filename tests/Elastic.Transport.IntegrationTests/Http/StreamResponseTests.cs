// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Elastic.Transport.IntegrationTests.Plumbing;
using Elastic.Transport.Products.Elasticsearch;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Elastic.Transport.IntegrationTests.Http;

public class StreamResponseTests(TransportTestServer instance) : AssemblyServerTestsBase(instance)
{
	private const string Path = "/streamresponse";

	[Fact]
	public async Task StreamResponse_ShouldNotBeDisposed()
	{
		var nodePool = new SingleNodePool(Server.Uri);
		var config = new TransportConfiguration(nodePool, productRegistration: new ElasticsearchProductRegistration(typeof(Clients.Elasticsearch.ElasticsearchClient)));
		var transport = new DistributedTransport(config);

		var response = await transport.PostAsync<StreamResponse>(Path, PostData.String("{}"));

		var sr = new StreamReader(response.Body);
		var responseString = sr.ReadToEndAsync();
	}
}

[ApiController, Route("[controller]")]
public class StreamResponseController : ControllerBase
{
	[HttpPost]
	public Task<JsonElement> Post([FromBody] JsonElement body) => Task.FromResult(body);
}
