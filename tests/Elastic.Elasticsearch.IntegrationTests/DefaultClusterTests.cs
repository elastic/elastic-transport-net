// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.Transport;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
using static Elastic.Transport.HttpMethod;

namespace Elastic.Elasticsearch.IntegrationTests;

public class DefaultClusterTests(DefaultCluster cluster, ITestOutputHelper output) : IntegrationTestBase(cluster, output)
{
	[Fact]
	public async Task AsyncRequestDoesNotThrow()
	{
		var response = await RequestHandler.RequestAsync<StringResponse>(GET, "/");
		_ = response.ApiCallDetails.Should().NotBeNull();
		_ = response.ApiCallDetails.HasSuccessfulStatusCode.Should().BeTrue();
	}

	[Fact]
	public void SyncRequestDoesNotThrow()
	{
		var response = RequestHandler.Request<StringResponse>(GET, "/");
		_ = response.ApiCallDetails.Should().NotBeNull();
		_ = response.ApiCallDetails.HasSuccessfulStatusCode.Should().BeTrue();
	}
}
