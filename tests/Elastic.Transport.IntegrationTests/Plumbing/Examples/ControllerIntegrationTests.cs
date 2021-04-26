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
			var response = await Transport.GetAsync<StringResponse>("/dummy/20");
			response.Success.Should().BeTrue("{0}", response.DebugInformation);
		}
	}

	[ApiController, Route("[controller]")]
	public class DummyController : ControllerBase
	{
		[HttpGet("{id}")]
		public async Task<int> Get(int id) => await Task.FromResult(id * 3);
	}

}
