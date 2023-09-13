// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

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
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Elastic.Transport.IntegrationTests.OpenTelemetry;

// We cannot allow these tests to run in parallel with other tests as the listener may pick up other activities.
[Collection(nameof(NonParallelCollection))]
public class OpenTelemetryTests : AssemblyServerTestsBase
{
	internal const string Cluster = "e9106fc68e3044f0b1475b04bf4ffd5f";
	internal const string Instance = "instance-0000000001";

	public OpenTelemetryTests(TransportTestServer instance) : base(instance) { }

	[Fact]
	public async Task ElasticsearchTagsShouldBeSet_WhenUsingTheElasticsearchRegistration()
	{
		var connection = new TestableHttpConnection();
		var connectionPool = new SingleNodePool(Server.Uri);
		var config = new TransportConfiguration(connectionPool, connection, productRegistration: new ElasticsearchProductRegistration(typeof(Clients.Elasticsearch.ElasticsearchClient)));
		var transport = new DistributedTransport(config);

		var mre = new ManualResetEvent(false);

		var callCounter = 0;
		using var listener = new ActivityListener()
		{
			ActivityStarted = _ => { },
			ActivityStopped = activity =>
			{
				callCounter++;

				if (callCounter > 1)
					Assert.Fail("Expected one activity, but received multiple stop events.");

				Assertions(activity);
				mre.Set();
			},
			ShouldListenTo = activitySource => activitySource.Name == Diagnostics.OpenTelemetry.ElasticTransportActivitySourceName,
			Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
		};
		ActivitySource.AddActivityListener(listener);

		_ = await transport.GetAsync<VoidResponse>("/opentelemetry");

		mre.WaitOne(TimeSpan.FromSeconds(1)).Should().BeTrue();

		static void Assertions(Activity activity)
		{
			var informationalVersion = (typeof(Clients.Elasticsearch.ElasticsearchClient)
				.Assembly
				.GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute), false)
				as AssemblyInformationalVersionAttribute[])?.FirstOrDefault()?.InformationalVersion;

			activity.TagObjects.Should().Contain(t => t.Key == OpenTelemetryAttributes.DbElasticsearchClusterName)
				.Subject.Value.Should().BeOfType<string>()
				.Subject.Should().Be(Cluster);

			activity.TagObjects.Should().Contain(t => t.Key == OpenTelemetryAttributes.DbElasticsearchNodeName)
				.Subject.Value.Should().BeOfType<string>()
				.Subject.Should().Be(Instance);

			activity.TagObjects.Should().Contain(t => t.Key == OpenTelemetryAttributes.ElasticTransportProductName)
				.Subject.Value.Should().BeOfType<string>()
				.Subject.Should().Be("elasticsearch-net");

			activity.TagObjects.Should().Contain(t => t.Key == OpenTelemetryAttributes.ElasticTransportProductVersion)
				.Subject.Value.Should().BeOfType<string>()
				.Subject.Should().Be(informationalVersion);
		}
	}
}

[ApiController, Route("[controller]")]
public class OpenTelemetryController : ControllerBase
{
	[HttpGet()]
	public Task Get()
	{
		Response.Headers.Add(ElasticsearchProductRegistration.XFoundHandlingClusterHeader, OpenTelemetryTests.Cluster);
		Response.Headers.Add(ElasticsearchProductRegistration.XFoundHandlingInstanceHeader, OpenTelemetryTests.Instance);

		return Task.CompletedTask;
	}
}

[CollectionDefinition(nameof(NonParallelCollection), DisableParallelization = true)]
public class NonParallelCollection { }
