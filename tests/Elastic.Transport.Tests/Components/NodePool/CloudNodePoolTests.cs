// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Linq;
using System.Text;
using FluentAssertions;
using Xunit;

namespace Elastic.Transport.Tests.Components.NodePool;

public class CloudNodePoolTests
{
	private static string EncodeCloudId(string clusterName, string decoded) =>
		$"{clusterName}:{Convert.ToBase64String(Encoding.UTF8.GetBytes(decoded))}";

	private static readonly ApiKey TestCredentials = new("dGVzdGtleQ==");

	[Fact]
	public void ParsesBasicCloudId()
	{
		var cloudId = EncodeCloudId("my-cluster", "us-east-1.aws.elastic.co$es-uuid$kibana-uuid");
		var pool = new CloudNodePool(cloudId, TestCredentials);

		pool.Nodes.First().Uri.Should().Be(new Uri("https://es-uuid.us-east-1.aws.elastic.co"));
	}

	[Fact]
	public void ParsesCloudIdTargetingElasticsearch()
	{
		var cloudId = EncodeCloudId("my-cluster", "us-east-1.aws.elastic.co$es-uuid$kibana-uuid");
		var pool = new CloudNodePool(cloudId, TestCredentials, CloudService.Elasticsearch);

		pool.Nodes.First().Uri.Should().Be(new Uri("https://es-uuid.us-east-1.aws.elastic.co"));
	}

	[Fact]
	public void ParsesCloudIdTargetingKibana()
	{
		var cloudId = EncodeCloudId("my-cluster", "us-east-1.aws.elastic.co$es-uuid$kibana-uuid");
		var pool = new CloudNodePool(cloudId, TestCredentials, CloudService.Kibana);

		pool.Nodes.First().Uri.Should().Be(new Uri("https://kibana-uuid.us-east-1.aws.elastic.co"));
	}

	[Fact]
	public void ParsesCloudIdWithCustomPortOnHost()
	{
		var cloudId = EncodeCloudId("my-cluster", "us-east-1.aws.elastic.co:9243$es-uuid$kibana-uuid");
		var pool = new CloudNodePool(cloudId, TestCredentials);

		pool.Nodes.First().Uri.Should().Be(new Uri("https://es-uuid.us-east-1.aws.elastic.co:9243"));
	}

	[Fact]
	public void ParsesCloudIdWithCustomPortOnHostTargetingKibana()
	{
		var cloudId = EncodeCloudId("my-cluster", "us-east-1.aws.elastic.co:9243$es-uuid$kibana-uuid");
		var pool = new CloudNodePool(cloudId, TestCredentials, CloudService.Kibana);

		pool.Nodes.First().Uri.Should().Be(new Uri("https://kibana-uuid.us-east-1.aws.elastic.co:9243"));
	}

	[Fact]
	public void ParsesCloudIdWithPerServicePorts()
	{
		var cloudId = EncodeCloudId("my-cluster", "us-east-1.aws.elastic.co:9243$es-uuid:9344$kibana-uuid:5601");
		var pool = new CloudNodePool(cloudId, TestCredentials);

		pool.Nodes.First().Uri.Should().Be(new Uri("https://es-uuid.us-east-1.aws.elastic.co:9344"));
	}

	[Fact]
	public void ParsesCloudIdWithPerServicePortsTargetingKibana()
	{
		var cloudId = EncodeCloudId("my-cluster", "us-east-1.aws.elastic.co:9243$es-uuid:9344$kibana-uuid:5601");
		var pool = new CloudNodePool(cloudId, TestCredentials, CloudService.Kibana);

		pool.Nodes.First().Uri.Should().Be(new Uri("https://kibana-uuid.us-east-1.aws.elastic.co:5601"));
	}

	[Fact]
	public void DefaultPortIs443AndOmittedFromUri()
	{
		var cloudId = EncodeCloudId("my-cluster", "us-east-1.aws.elastic.co$es-uuid$kibana-uuid");
		var pool = new CloudNodePool(cloudId, TestCredentials);

		var uri = pool.Nodes.First().Uri;
		uri.Port.Should().Be(443);
		uri.ToString().Should().NotContain(":443");
	}

	[Fact]
	public void ThrowsWhenKibanaUuidMissingAndTargetingKibana()
	{
		var cloudId = EncodeCloudId("my-cluster", "us-east-1.aws.elastic.co$es-uuid");

		var act = () => new CloudNodePool(cloudId, TestCredentials, CloudService.Kibana);
		act.Should().Throw<ArgumentException>().WithMessage("*Kibana*");
	}

	[Fact]
	public void ThrowsWhenKibanaUuidEmptyAndTargetingKibana()
	{
		var cloudId = EncodeCloudId("my-cluster", "us-east-1.aws.elastic.co$es-uuid$");

		var act = () => new CloudNodePool(cloudId, TestCredentials, CloudService.Kibana);
		act.Should().Throw<ArgumentException>().WithMessage("*Kibana*");
	}

	[Fact]
	public void ParsesCloudIdWithOnlyTwoPartsForElasticsearch()
	{
		var cloudId = EncodeCloudId("my-cluster", "us-east-1.aws.elastic.co$es-uuid");
		var pool = new CloudNodePool(cloudId, TestCredentials);

		pool.Nodes.First().Uri.Should().Be(new Uri("https://es-uuid.us-east-1.aws.elastic.co"));
	}

	[Fact]
	public void ThrowsOnEmptyCloudId()
	{
		var act = () => new CloudNodePool("", TestCredentials);
		act.Should().Throw<ArgumentException>();
	}

	[Fact]
	public void ThrowsOnMissingBase64Data()
	{
		var act = () => new CloudNodePool("my-cluster:", TestCredentials);
		act.Should().Throw<ArgumentException>();
	}

	[Fact]
	public void ThrowsOnMissingElasticsearchUuid()
	{
		var cloudId = EncodeCloudId("my-cluster", "us-east-1.aws.elastic.co$");

		var act = () => new CloudNodePool(cloudId, TestCredentials);
		act.Should().Throw<ArgumentException>();
	}

	[Fact]
	public void OldTwoParamConstructorStillWorks()
	{
		var cloudId = EncodeCloudId("my-cluster", "us-east-1.aws.elastic.co$es-uuid$kibana-uuid");
		var pool = new CloudNodePool(cloudId, TestCredentials);

		pool.AuthenticationHeader.Should().NotBeNull();
		pool.Nodes.First().Uri.Should().Be(new Uri("https://es-uuid.us-east-1.aws.elastic.co"));
	}

	[Fact]
	public void UriConstructorStillWorks()
	{
		var pool = new CloudNodePool(new Uri("https://my-cluster.elastic.co"), TestCredentials);

		pool.AuthenticationHeader.Should().NotBeNull();
		pool.Nodes.First().Uri.Should().Be(new Uri("https://my-cluster.elastic.co"));
	}

	[Fact]
	public void HostPortOverrideByServicePort()
	{
		var cloudId = EncodeCloudId("my-cluster", "host.elastic.co:9243$es-uuid:9344$kibana-uuid");
		var esPool = new CloudNodePool(cloudId, TestCredentials, CloudService.Elasticsearch);
		var kbPool = new CloudNodePool(cloudId, TestCredentials, CloudService.Kibana);

		esPool.Nodes.First().Uri.Port.Should().Be(9344);
		kbPool.Nodes.First().Uri.Port.Should().Be(9243);
	}
}
