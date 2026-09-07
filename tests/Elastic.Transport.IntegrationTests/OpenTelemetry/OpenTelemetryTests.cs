// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

#nullable enable

using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Elastic.Transport.Diagnostics;
using Elastic.Transport.IntegrationTests.Plumbing;
using Elastic.Transport.IntegrationTests.Plumbing.Stubs;
using Elastic.Transport.Products.Elasticsearch;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Elastic.Transport.IntegrationTests.OpenTelemetry;

// We cannot allow these tests to run in parallel with other tests as the listener may pick up other activities.
[Collection(nameof(NonParallel))]
public class OpenTelemetryTests(TestServerFixture instance) : AssemblyServerTestsBase(instance)
{
	internal const string Cluster = "e9106fc68e3044f0b1475b04bf4ffd5f";
	internal const string Instance = "instance-0000000001";
	internal const string OnPremCluster = "my-onprem-cluster";

	[Fact]
	public async Task ElasticsearchTagsShouldBeSetWhenUsingTheElasticsearchRegistration()
	{
		var (activity, _) = await CaptureActivityAsync("/opentelemetry");

		var informationalVersion = (typeof(Clients.Elasticsearch.ElasticsearchClient)
			.Assembly
			.GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute), false)
			as AssemblyInformationalVersionAttribute[])?.FirstOrDefault()?.InformationalVersion;

		TagValue(activity, OpenTelemetryAttributes.DbElasticsearchClusterName).Should().Be(Cluster);
		TagValue(activity, OpenTelemetryAttributes.DbElasticsearchNodeName).Should().Be(Instance);
		TagValue(activity, OpenTelemetryAttributes.ElasticTransportProductName).Should().Be("elasticsearch-net");
		TagValue(activity, OpenTelemetryAttributes.ElasticTransportProductVersion).Should().Be(informationalVersion);
	}

	[Fact]
	public async Task ClusterNameIsTakenFromElasticClusterNameHeaderWhenCloudHeaderIsAbsent()
	{
		var (activity, _) = await CaptureActivityAsync("/opentelemetry/onprem");

		TagValue(activity, OpenTelemetryAttributes.DbElasticsearchClusterName).Should().Be(OnPremCluster);
		_ = activity.TagObjects.Should().NotContain(t => t.Key == OpenTelemetryAttributes.DbElasticsearchNodeName);
	}

	[Fact]
	public async Task CloudClusterHeaderTakesPrecedenceOverElasticClusterNameHeader()
	{
		var (activity, _) = await CaptureActivityAsync("/opentelemetry/both");

		TagValue(activity, OpenTelemetryAttributes.DbElasticsearchClusterName).Should().Be(Cluster);
	}

	[Fact]
	public async Task ParsingAllHeadersAlongsideTelemetryHeadersSucceeds()
	{
		var (_, response) = await CaptureActivityAsync("/opentelemetry", new RequestConfiguration { ParseAllHeaders = true });

		_ = response.ApiCallDetails.OriginalException.Should().BeNull();
		_ = response.ApiCallDetails.HttpStatusCode.Should().Be(200);
		_ = response.ApiCallDetails.TryGetHeader(ElasticsearchProductRegistration.XFoundHandlingClusterHeader, out var values).Should().BeTrue();
		_ = values.Should().ContainSingle().Which.Should().Be(Cluster);
	}

	[Fact]
	public async Task ExplicitlyParsingATelemetryHeaderSucceeds()
	{
		var (_, response) = await CaptureActivityAsync("/opentelemetry/onprem",
			new RequestConfiguration { ResponseHeadersToParse = new HeadersList("Elastic-Cluster-Name") });

		_ = response.ApiCallDetails.OriginalException.Should().BeNull();
		_ = response.ApiCallDetails.HttpStatusCode.Should().Be(200);
		_ = response.ApiCallDetails.TryGetHeader("Elastic-Cluster-Name", out var values).Should().BeTrue();
		_ = values.Should().ContainSingle().Which.Should().Be(OnPremCluster);
	}

	private static string? TagValue(Activity activity, string key) =>
		activity.TagObjects.Should().Contain(t => t.Key == key).Subject.Value.Should().BeOfType<string>().Subject;

	/// <summary>
	/// Issues a single request through a fresh Elasticsearch registration and returns the stopped transport
	/// activity together with the response. A fresh registration per call keeps the cached cluster name from
	/// leaking between tests.
	/// </summary>
	private async Task<(Activity Activity, TransportResponse Response)> CaptureActivityAsync(string path, IRequestConfiguration? requestConfiguration = null)
	{
		var requestInvoker = new TrackingRequestInvoker();
		var nodePool = new SingleNodePool(Server.Uri);
		var config = new TransportConfiguration(nodePool, requestInvoker, productRegistration: new ElasticsearchProductRegistration(typeof(Clients.Elasticsearch.ElasticsearchClient)));
		var transport = new DistributedTransport(config);

		var mre = new ManualResetEvent(false);
		Activity? stopped = null;
		var callCounter = 0;

		using var listener = new ActivityListener
		{
			ActivityStarted = _ => { },
			ActivityStopped = activity =>
			{
				callCounter++;

				if (callCounter > 1)
					Assert.Fail("Expected one activity, but received multiple stop events.");

				stopped = activity;
				_ = mre.Set();
			},
			ShouldListenTo = activitySource => activitySource.Name == Diagnostics.OpenTelemetry.ElasticTransportActivitySourceName,
			Sample = (ref _) => ActivitySamplingResult.AllData
		};
		ActivitySource.AddActivityListener(listener);

		var response = await transport.RequestAsync<VoidResponse>(
			new EndpointPath(HttpMethod.GET, path), postData: null, null, requestConfiguration, TestContext.Current.CancellationToken);

		_ = mre.WaitOne(TimeSpan.FromSeconds(1)).Should().BeTrue();
		_ = stopped.Should().NotBeNull();

		return (stopped!, response);
	}
}

[ApiController, Route("[controller]")]
public class OpenTelemetryController : ControllerBase
{
	[HttpGet()]
	public Task Get()
	{
		Response.Headers.Append(ElasticsearchProductRegistration.XFoundHandlingClusterHeader, OpenTelemetryTests.Cluster);
		Response.Headers.Append(ElasticsearchProductRegistration.XFoundHandlingInstanceHeader, OpenTelemetryTests.Instance);

		return Task.CompletedTask;
	}

	[HttpGet("onprem")]
	public Task OnPrem()
	{
		Response.Headers.Append("Elastic-Cluster-Name", OpenTelemetryTests.OnPremCluster);

		return Task.CompletedTask;
	}

	[HttpGet("both")]
	public Task Both()
	{
		Response.Headers.Append(ElasticsearchProductRegistration.XFoundHandlingClusterHeader, OpenTelemetryTests.Cluster);
		Response.Headers.Append("Elastic-Cluster-Name", OpenTelemetryTests.OnPremCluster);

		return Task.CompletedTask;
	}
}

[CollectionDefinition(nameof(NonParallel), DisableParallelization = true)]
public class NonParallel { }
