// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.Transport;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
using static Elastic.Transport.HttpMethod;

namespace Elastic.Elasticsearch.IntegrationTests;

public class SecurityClusterTests : IntegrationTestBase<SecurityCluster>
{
	public SecurityClusterTests(SecurityCluster cluster, ITestOutputHelper output) : base(cluster, output) { }

	[Fact]
	public async Task AsyncRequestDoesNotThrow()
	{
		var response = await Transport.RequestAsync<StringResponse>(GET, "/");
		response.ApiCallDetails.Should().NotBeNull();
		response.ApiCallDetails.HasSuccessfulStatusCode.Should().BeTrue();
	}

	[Fact]
	public void SyncRequestDoesNotThrow()
	{
		var response = Transport.Request<StringResponse>(GET, "/");
		response.ApiCallDetails.Should().NotBeNull();
		response.ApiCallDetails.HasSuccessfulStatusCode.Should().BeTrue();
	}

	[Fact]
	public void SyncRequestDoesNotThrowOnBadAuth()
	{
		var response = Transport.Request<StringResponse>(GET, "/", null, new DefaultRequestParameters
		{
			RequestConfiguration = new RequestConfiguration
			{
				AuthenticationHeader = new BasicAuthentication("unknown-user", "bad-password")
			}
		});
		response.ApiCallDetails.Should().NotBeNull();
		response.ApiCallDetails.HasSuccessfulStatusCode.Should().BeFalse();
	}
}
