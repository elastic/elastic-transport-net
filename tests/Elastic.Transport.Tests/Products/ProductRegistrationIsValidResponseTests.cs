// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.Transport.Products;
using Elastic.Transport.Products.Elasticsearch;
using FluentAssertions;
using Xunit;

namespace Elastic.Transport.Tests.Products;

public class ProductRegistrationIsValidResponseTests
{
	[Fact]
	public void DefaultRegistrationReturnsFalseForNullDetails() =>
		DefaultProductRegistration.Default.IsValidResponse(null).Should().BeFalse();

	[Fact]
	public void DefaultRegistrationReturnsTrueForSuccessAndExpectedContentType() =>
		DefaultProductRegistration.Default
			.IsValidResponse(MakeDetails(200, hasSuccess: true))
			.Should().BeTrue();

	[Fact]
	public void DefaultRegistrationReturnsFalseForNonSuccess() =>
		DefaultProductRegistration.Default
			.IsValidResponse(MakeDetails(404, hasSuccess: false))
			.Should().BeFalse();

	[Fact]
	public void DefaultRegistrationReturnsFalseForUnexpectedContentType()
	{
		var details = MakeDetails(200, hasSuccess: true);
		details.HasExpectedContentType = false;
		DefaultProductRegistration.Default.IsValidResponse(details).Should().BeFalse();
	}

	[Fact]
	public void ElasticsearchReturnsTrueFor404WithNoExtractedServerError() =>
		ElasticsearchProductRegistration.Default
			.IsValidResponse(MakeDetails(404, hasSuccess: false))
			.Should().BeTrue();

	[Fact]
	public void ElasticsearchReturnsFalseFor404WithExtractedServerError()
	{
		var details = MakeDetails(404, hasSuccess: false);
		details.ProductError = new ElasticsearchServerError(
			new Error { Type = "index_not_found_exception", Reason = "no such index" },
			statusCode: 404);
		ElasticsearchProductRegistration.Default.IsValidResponse(details).Should().BeFalse();
	}

	[Fact]
	public void ElasticsearchReturnsFalseFor500WithExtractedServerError()
	{
		var details = MakeDetails(500, hasSuccess: false);
		details.ProductError = new ElasticsearchServerError(
			new Error { Type = "internal", Reason = "boom" },
			statusCode: 500);
		ElasticsearchProductRegistration.Default.IsValidResponse(details).Should().BeFalse();
	}

	[Fact]
	public void ElasticsearchReturnsTrueForSuccess() =>
		ElasticsearchProductRegistration.Default
			.IsValidResponse(MakeDetails(200, hasSuccess: true))
			.Should().BeTrue();

	[Fact]
	public void ElasticsearchReturnsFalseFor404WithUnexpectedContentType()
	{
		var details = MakeDetails(404, hasSuccess: false);
		details.HasExpectedContentType = false;
		ElasticsearchProductRegistration.Default.IsValidResponse(details).Should().BeFalse();
	}

	private static ApiCallDetails MakeDetails(int statusCode, bool hasSuccess) =>
		new()
		{
			HttpStatusCode = statusCode,
			HasSuccessfulStatusCode = hasSuccess,
			HasExpectedContentType = true,
		};
}
