/*
 * Licensed to Elasticsearch B.V. under one or more contributor
 * license agreements. See the NOTICE file distributed with
 * this work for additional information regarding copyright
 * ownership. Elasticsearch B.V. licenses this file to you under
 * the Apache License, Version 2.0 (the "License"); you may
 * not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *    http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing,
 * software distributed under the License is distributed on an
 * "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
 * KIND, either express or implied.  See the License for the
 * specific language governing permissions and limitations
 * under the License.
 */

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
	public class EndpointIntegrationTests : ClassServerTestsBase<TransportTestServer<DummyStartup>>
	{
		public EndpointIntegrationTests(TransportTestServer<DummyStartup> instance) : base(instance) { }

		[Fact]
		public async Task CanCallIntoEndpoint()
		{
			var response = await Transport.GetAsync<StringResponse>(DummyStartup.Endpoint);
			response.Success.Should().BeTrue("{0}", response.DebugInformation);
		}
	}

	public class DummyStartup : DefaultStartup
	{
		public DummyStartup(IConfiguration configuration) : base(configuration) { }

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
