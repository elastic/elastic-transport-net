// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

#nullable enable

using Elastic.Transport.Products;
using FluentAssertions;
using Xunit;

namespace Elastic.Transport.Tests.Products;

public class DefaultProductRegistrationContentTypeTests
{
	[Theory]
	[InlineData("application/json", "application/json")]
	[InlineData("application/json", "application/json;charset=utf-8")]
	[InlineData("application/json", "APPLICATION/JSON")]
	[InlineData("text/event-stream", "text/event-stream")]
	[InlineData("application/vnd.elasticsearch+json;compatible-with=8", "application/vnd.elasticsearch+json;compatible-with=8")]
	public void ReturnsTrueForExactOrPrefixMatch(string accept, string responseContentType) =>
		DefaultProductRegistration.Default.IsExpectedResponseContentType(accept, responseContentType).Should().BeTrue();

	[Theory]
	[InlineData("application/json", "text/plain")]
	[InlineData("application/json", "application/xml")]
	[InlineData("application/vnd.elasticsearch+json;compatible-with=8", "application/json")]
	[InlineData("application/vnd.elasticsearch+json;compatible-with=8", "application/vnd.elasticsearch+json")]
	[InlineData("application/json", null)]
	[InlineData("application/json", "")]
	public void ReturnsFalseForMismatchOrEmpty(string accept, string? responseContentType) =>
		DefaultProductRegistration.Default.IsExpectedResponseContentType(accept, responseContentType).Should().BeFalse();
}
