// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Elastic.Transport.IntegrationTests.Plumbing.Examples
{
	/// <summary>
	/// Spins up a server just for the tests in this class, using a custom startup we attach a special endpoint handler
	/// Since it extends <see cref="ClassServerTestsBase{TServer}"/> it server is only shared between the tests inside this class.
	/// The server is also started and stopped after all the tests in this class run.
	/// </summary>
	public class EndpointIntegrationTests(BufferedServerFixture instance)
		: ClassServerTestsBase<BufferedServerFixture>(instance)
	{
		[Fact]
		public async Task CanCallIntoEndpoint()
		{
			var response = await RequestHandler.GetAsync<StringResponse>(BufferedStartup.Endpoint, cancellationToken: TestContext.Current.CancellationToken);
			response.ApiCallDetails.HasSuccessfulStatusCode.Should().BeTrue("{0}", response.ApiCallDetails.DebugInformation);
		}
	}

	public class BufferedServerFixture : TransportTestServer<BufferedStartup>;
	public class BufferedStartup : DefaultStartup
	{
		public BufferedStartup(IConfiguration configuration) : base(configuration) { }

		public static string Endpoint { get; } = "buffered";

		protected override void MapEndpoints(IEndpointRouteBuilder endpoints) =>
			endpoints.MapGet("/buffered", async context =>
			{
				var name = context.Request.RouteValues["id"];
				await context.Response.WriteAsync($"Hello {name}!");
				await Task.Delay(1);
				await context.Response.WriteAsync($"World!");
			});
	}
}
