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
	private sealed class TestableResponse : ElasticsearchResponse { }

	[Fact]
	public void CreateSuccessfulResponseShouldBeValid()
	{
		var response = TestableResponseFactory.CreateSuccessfulResponse<TestableResponse>(new(), 200);
		_ = response.IsValidResponse.Should().BeTrue();
	}

	[Fact]
	public void CreateSuccessfulResponseApiCallDetailsShouldContainStatusCode()
	{
		var response = TestableResponseFactory.CreateSuccessfulResponse<TestableResponse>(new(), 200);
		_ = response.ApiCallDetails.HttpStatusCode.Should().Be(200);
	}

	[Fact]
	public void CreateSuccessfulResponseApiCallDetailsShouldBeSuccessful()
	{
		var response = TestableResponseFactory.CreateSuccessfulResponse<TestableResponse>(new(), 200);
		_ = response.ApiCallDetails.HasSuccessfulStatusCode.Should().BeTrue();
	}

	[Fact]
	public void CreateResponseGivenBadResponseShouldNotBeValid()
	{
		var response = TestableResponseFactory.CreateResponse<TestableResponse>(new(), 400, false);
		_ = response.IsValidResponse.Should().BeFalse();
	}

	[Fact]
	public void CreateResponseApiCallDetailsShouldContainStatusCode()
	{
		var response = TestableResponseFactory.CreateResponse<TestableResponse>(new(), 400, false);
		_ = response.ApiCallDetails.HttpStatusCode.Should().Be(400);
	}

	[Fact]
	public void CreateResponseApiCallDetailsShouldNotBeSuccessful()
	{
		var response = TestableResponseFactory.CreateResponse<TestableResponse>(new(), 400, false);
		_ = response.ApiCallDetails.HasSuccessfulStatusCode.Should().BeFalse();
	}

	[Fact]
	public void CreateResponseResponseShouldIncludeDebugInfo()
	{
		var response = TestableResponseFactory.CreateResponse<TestableResponse>(new(), 400, false);
		_ = response.DebugInformation.Should().NotBeNull();
	}

	[Fact]
	public void CreateResponseOriginalExceptionShouldBeNull()
	{
		var response = TestableResponseFactory.CreateResponse<TestableResponse>(new(), 400, false);
		_ = response.TryGetOriginalException(out var exception);
		_ = exception.Should().BeNull();
	}

	[Fact]
	public void CreateResponseElasticsearchServerErrorShouldBeNull()
	{
		var response = TestableResponseFactory.CreateResponse<TestableResponse>(new(), 400, false);
		_ = response.ElasticsearchServerError.Should().BeNull();
	}

	[Fact]
	public void CreateResponseElasticsearchWarningsShouldBeEmpty()
	{
		var response = TestableResponseFactory.CreateResponse<TestableResponse>(new(), 400, false);
		_ = response.ElasticsearchWarnings.Should().BeEmpty();
	}
}
