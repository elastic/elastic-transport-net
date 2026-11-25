// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

#nullable enable

using Elastic.Transport.Products.Elasticsearch;
using FluentAssertions;
using Xunit;

namespace Elastic.Transport.Tests.Products.Elasticsearch;

public class IsExpectedResponseContentTypeTests
{
	[Theory]
	// Exact match
	[InlineData("application/vnd.elasticsearch+json;compatible-with=8", "application/vnd.elasticsearch+json;compatible-with=8")]
	// Response is more verbose (extra parameters)
	[InlineData("application/vnd.elasticsearch+json;compatible-with=8", "application/vnd.elasticsearch+json;compatible-with=8;charset=utf-8")]
	// Vendor MIME with compat-with stripped from the response
	[InlineData("application/vnd.elasticsearch+json;compatible-with=8", "application/vnd.elasticsearch+json")]
	// Bare form (the part after the +)
	[InlineData("application/vnd.elasticsearch+json;compatible-with=8", "application/json")]
	[InlineData("application/vnd.elasticsearch+json;compatible-with=8", "application/json;charset=utf-8")]
	// x-ndjson bare form
	[InlineData("application/vnd.elasticsearch+x-ndjson;compatible-with=8", "application/vnd.elasticsearch+x-ndjson")]
	[InlineData("application/vnd.elasticsearch+x-ndjson;compatible-with=8", "application/x-ndjson")]
	// Canonical mapbox bare form (the SearchMvt path)
	[InlineData("application/vnd.elasticsearch+vnd.mapbox-vector-tile;compatible-with=8", "application/vnd.mapbox-vector-tile")]
	[InlineData("application/vnd.elasticsearch+vnd.mapbox-vector-tile;compatible-with=8", "application/vnd.elasticsearch+vnd.mapbox-vector-tile")]
	// Multi-value Accept — any vendor entry's bare form is acceptable
	[InlineData("application/vnd.elasticsearch+json,application/vnd.elasticsearch+x-ndjson", "application/x-ndjson")]
	[InlineData("application/vnd.elasticsearch+json,application/vnd.elasticsearch+x-ndjson", "application/json")]
	// Case-insensitive
	[InlineData("application/vnd.elasticsearch+json;compatible-with=8", "APPLICATION/JSON")]
	public void Accepts(string accept, string responseContentType) =>
		new ElasticsearchProductRegistration(typeof(ElasticsearchProductRegistration))
			.IsExpectedResponseContentType(accept, responseContentType).Should().BeTrue();

	[Theory]
	// Plain Accept — bare-form rule must NOT trigger; only exact / prefix matches.
	[InlineData("application/json", "text/plain")]
	[InlineData("text/event-stream", "application/json")]
	// Non-elasticsearch vendor MIME as Accept — bare-form rule must NOT trigger
	// (third-party vendor MIMEs aren't owned by this product).
	[InlineData("application/vnd.mapbox-vector-tile", "application/json")]
	[InlineData("application/vnd.mapbox-vector-tile;compatible-with=8", "application/json")]
	// Vendor Accept — completely different MIME on response
	[InlineData("application/vnd.elasticsearch+json;compatible-with=8", "text/html")]
	[InlineData("application/vnd.elasticsearch+x-ndjson;compatible-with=8", "application/json")]
	// Empty / null response
	[InlineData("application/vnd.elasticsearch+json;compatible-with=8", null)]
	[InlineData("application/vnd.elasticsearch+json;compatible-with=8", "")]
	public void Rejects(string accept, string? responseContentType) =>
		new ElasticsearchProductRegistration(typeof(ElasticsearchProductRegistration))
			.IsExpectedResponseContentType(accept, responseContentType).Should().BeFalse();
}
