// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.Transport.Products.Elasticsearch;
using FluentAssertions;
using Xunit;

namespace Elastic.Transport.Tests;

public class TestableResponseFactoryTests
{
	/// <summary>
	/// Class used for testing purposes only
	/// </summary>
	private class TestableResponse : ElasticsearchResponse { }

	[Fact]
	public void CreateSuccessfulResponse_ShouldBeValid()
	{
		var response = TestableResponseFactory.CreateSuccessfulResponse<TestableResponse>(new(), 200);
		response.IsValidResponse.Should().BeTrue();
	}

	[Fact]
	public void CreateSuccessfulResponse_ApiCallDetailsShouldContainStatusCode()
	{
		var response = TestableResponseFactory.CreateSuccessfulResponse<TestableResponse>(new(), 200);
		response.ApiCallDetails.HttpStatusCode.Should().Be(200);
	}

	[Fact]
	public void CreateSuccessfulResponse_ApiCallDetailsShouldBeSuccessful()
	{
		var response = TestableResponseFactory.CreateSuccessfulResponse<TestableResponse>(new(), 200);
		response.ApiCallDetails.HasSuccessfulStatusCode.Should().BeTrue();
	}

	[Fact]
	public void CreateResponse_GivenBadResponse_ShouldNotBeValid()
	{
		var response = TestableResponseFactory.CreateResponse<TestableResponse>(new(), 400, false);
		response.IsValidResponse.Should().BeFalse();
	}

	[Fact]
	public void CreateResponse_ApiCallDetailsShouldContainStatusCode()
	{
		var response = TestableResponseFactory.CreateResponse<TestableResponse>(new(), 400, false);
		response.ApiCallDetails.HttpStatusCode.Should().Be(400);
	}

	[Fact]
	public void CreateResponse_ApiCallDetailsShouldNotBeSuccessful()
	{
		var response = TestableResponseFactory.CreateResponse<TestableResponse>(new(), 400, false);
		response.ApiCallDetails.HasSuccessfulStatusCode.Should().BeFalse();
	}

	[Fact]
	public void CreateResponse_ResponseShouldIncludeDebugInfo()
	{
		var response = TestableResponseFactory.CreateResponse<TestableResponse>(new(), 400, false);
		response.DebugInformation.Should().NotBeNull();
	}

	[Fact]
	public void CreateResponse_OriginalExceptionShouldBeNull()
	{
		var response = TestableResponseFactory.CreateResponse<TestableResponse>(new(), 400, false);
		response.TryGetOriginalException(out var exception);
		exception.Should().BeNull();
	}

	[Fact]
	public void CreateResponse_ElasticsearchServerErrorShouldBeNull()
	{
		var response = TestableResponseFactory.CreateResponse<TestableResponse>(new(), 400, false);
		response.ElasticsearchServerError.Should().BeNull();
	}

	[Fact]
	public void CreateResponse_ElasticsearchWarningsShouldBeEmpty()
	{
		var response = TestableResponseFactory.CreateResponse<TestableResponse>(new(), 400, false);
		response.ElasticsearchWarnings.Should().BeEmpty();
	}
}
