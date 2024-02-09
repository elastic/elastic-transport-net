// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Xunit;

// Feel free to delete these tests at some point, these are here as examples while we built out our test suite
// A lot of integration tests need to be ported from elastic/elasticsearch-net
namespace Elastic.Transport.IntegrationTests.Plumbing.Examples
{
	/// <summary>
	/// Tests that the test framework loads a controller and the exposed transport can talk to its endpoints.
	/// Tests runs against a server that started up once and its server instance shared among many test classes
	/// </summary>
	public class ControllerIntegrationTests : AssemblyServerTestsBase
	{
		public ControllerIntegrationTests(TransportTestServer instance) : base(instance) { }

		[Fact]
		public async Task CanCallIntoController()
		{
			var response = await RequestHandler.GetAsync<StringResponse>("/dummy/20");
			response.ApiCallDetails.HasSuccessfulStatusCode.Should().BeTrue("{0}", response.ApiCallDetails.DebugInformation);
		}
	}

	[ApiController, Route("[controller]")]
	public class DummyController : ControllerBase
	{
		[HttpGet("{id}")]
		public async Task<int> Get(int id) => await Task.FromResult(id * 3);
	}

}
