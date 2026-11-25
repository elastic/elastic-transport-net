// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

#nullable enable

using System.Collections.Generic;
using System.Diagnostics;
using Elastic.Transport.Diagnostics;
using Elastic.Transport.Products.Elasticsearch;
using FluentAssertions;
using Xunit;

namespace Elastic.Transport.Tests.Products.Elasticsearch;

public class ClusterNameAttributeTests
{
	// Header names are spelled out rather than referencing the registration's constants so these
	// tests pin the wire contract with the Elastic Cloud proxy and the Elasticsearch server.
	private const string CloudClusterHeader = "X-Found-Handling-Cluster";
	private const string CloudInstanceHeader = "X-Found-Handling-Instance";
	private const string ServerClusterHeader = "Elastic-Cluster-Name";

	private const string CloudCluster = "e9106fc68e3044f0b1475b04bf4ffd5f";
	private const string OnPremCluster = "my-onprem-cluster";

	private static ElasticsearchProductRegistration NewRegistration() =>
		new(typeof(ElasticsearchProductRegistration));

	private static ApiCallDetails Details(params (string Name, string Value)[] headers)
	{
		var parsed = new Dictionary<string, IEnumerable<string>>();
		foreach (var (name, value) in headers)
			parsed[name] = [value];

		return new ApiCallDetails { ParsedHeaders = parsed };
	}

	[Fact]
	public void ElasticClusterNameHeaderIsRecordedAsClusterName()
	{
		var attributes = NewRegistration()
			.ParseOpenTelemetryAttributesFromApiCallDetails(Details((ServerClusterHeader, OnPremCluster)));

		attributes.Should().ContainKey(OpenTelemetryAttributes.DbElasticsearchClusterName)
			.WhoseValue.Should().Be(OnPremCluster);
	}

	[Fact]
	public void XFoundHandlingClusterTakesPrecedenceOverElasticClusterName()
	{
		var attributes = NewRegistration()
			.ParseOpenTelemetryAttributesFromApiCallDetails(Details(
				(ServerClusterHeader, OnPremCluster),
				(CloudClusterHeader, CloudCluster)));

		attributes.Should().ContainKey(OpenTelemetryAttributes.DbElasticsearchClusterName)
			.WhoseValue.Should().Be(CloudCluster);
	}

	[Fact]
	public void MissingClusterHeadersYieldNoClusterNameAttribute()
	{
		var attributes = NewRegistration()
			.ParseOpenTelemetryAttributesFromApiCallDetails(Details((CloudInstanceHeader, "instance-0000000001")));

		attributes.Should().NotContainKey(OpenTelemetryAttributes.DbElasticsearchClusterName);
		attributes.Should().ContainKey(OpenTelemetryAttributes.DbElasticsearchNodeName)
			.WhoseValue.Should().Be("instance-0000000001");
	}

	[Fact]
	public void ClusterNameIsCachedPerRegistrationInstance()
	{
		var first = NewRegistration();
		var second = NewRegistration();

		_ = first.ParseOpenTelemetryAttributesFromApiCallDetails(Details((ServerClusterHeader, "cluster-a")));
		var attributes = second.ParseOpenTelemetryAttributesFromApiCallDetails(Details((ServerClusterHeader, "cluster-b")));

		attributes.Should().ContainKey(OpenTelemetryAttributes.DbElasticsearchClusterName)
			.WhoseValue.Should().Be("cluster-b");
	}

	[Fact]
	public void CachedClusterNameIsReusedForResponsesWithoutClusterHeaders()
	{
		var registration = NewRegistration();

		_ = registration.ParseOpenTelemetryAttributesFromApiCallDetails(Details((ServerClusterHeader, OnPremCluster)));
		var attributes = registration.ParseOpenTelemetryAttributesFromApiCallDetails(Details());

		attributes.Should().ContainKey(OpenTelemetryAttributes.DbElasticsearchClusterName)
			.WhoseValue.Should().Be(OnPremCluster);
	}

	[Fact]
	public void DefaultHeadersToParseRequestsBothClusterHeadersUntilResolved()
	{
		using var listener = new ActivityListener
		{
			ActivityStarted = _ => { },
			ActivityStopped = _ => { },
			ShouldListenTo = source => source.Name == OpenTelemetry.ElasticTransportActivitySourceName,
			Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
		};
		ActivitySource.AddActivityListener(listener);
		using var activity = OpenTelemetry.ElasticTransportActivitySource.StartActivity("test");
		activity.Should().NotBeNull();

		var registration = NewRegistration();

		registration.DefaultHeadersToParse().Should()
			.BeEquivalentTo([CloudClusterHeader, ServerClusterHeader, CloudInstanceHeader]);

		_ = registration.ParseOpenTelemetryAttributesFromApiCallDetails(Details((ServerClusterHeader, OnPremCluster)));

		registration.DefaultHeadersToParse().Should().BeEquivalentTo([CloudInstanceHeader]);
	}
}
