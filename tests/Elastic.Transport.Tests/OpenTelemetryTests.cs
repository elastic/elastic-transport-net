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
using Elastic.Transport.Tests.Plumbing;
using FluentAssertions;
using Xunit;

namespace Elastic.Transport.Tests;

// We cannot allow these tests to run in parallel with other tests as the listener may pick up other activities.
[Collection(nameof(NonParallelCollection))]
public class OpenTelemetryTests
{
	[Fact]
	public async Task DefaultTagsShouldBeSet()
	{
		await TestCoreAsync(Assertions);

		static void Assertions(Activity activity)
		{
			var informationalVersion = (typeof(DistributedTransport)
				.Assembly
				.GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute), false)
				as AssemblyInformationalVersionAttribute[])?.FirstOrDefault()?.InformationalVersion;

			activity.Should().NotBeNull();
			activity.Kind.Should().Be(ActivityKind.Client);
			activity.DisplayName.Should().Be("GET");
			activity.OperationName.Should().Be("GET");

			activity.TagObjects.Should().Contain(t => t.Key == SemanticConventions.UrlFull)
				.Subject.Value.Should().BeOfType<string>()
				.Subject.Should().Be("http://localhost:9200/");

			activity.TagObjects.Should().Contain(t => t.Key == SemanticConventions.ServerAddress)
				.Subject.Value.Should().BeOfType<string>()
				.Subject.Should().Be("localhost");

			activity.TagObjects.Should().Contain(t => t.Key == SemanticConventions.ServerPort)
				.Subject.Value.Should().BeOfType<int>()
				.Subject.Should().Be(9200);

			activity.TagObjects.Should().Contain(t => t.Key == SemanticConventions.HttpRequestMethod)
				.Subject.Value.Should().BeOfType<string>()
				.Subject.Should().Be("GET");

			activity.TagObjects.Should().Contain(t => t.Key == SemanticConventions.HttpResponseStatusCode)
				.Subject.Value.Should().BeOfType<int>()
				.Subject.Should().Be(200);

			activity.TagObjects.Should().Contain(t => t.Key == SemanticConventions.UserAgentOriginal)
				.Subject.Value.Should().BeOfType<string>()
				.Subject.Should().StartWith($"elastic-transport-net/{informationalVersion}");

			activity.TagObjects.Should().Contain(t => t.Key == OpenTelemetryAttributes.ElasticTransportAttemptedNodes)
				.Subject.Value.Should().BeOfType<int>()
				.Subject.Should().Be(1);

			activity.TagObjects.Should().Contain(t => t.Key == OpenTelemetryAttributes.ElasticTransportVersion)
				.Subject.Value.Should().BeOfType<string>()
				.Subject.Should().Be(informationalVersion);

			activity.TagObjects.Should().Contain(t => t.Key == OpenTelemetryAttributes.ElasticTransportProductVersion)
				.Subject.Value.Should().BeOfType<string>()
				.Subject.Should().Be(informationalVersion);

			activity.TagObjects.Should().Contain(t => t.Key == OpenTelemetryAttributes.ElasticTransportProductName)
				.Subject.Value.Should().BeOfType<string>()
				.Subject.Should().Be("elastic-transport-net");

			activity.TagObjects.Should().Contain(t => t.Key == OpenTelemetryAttributes.ElasticTransportSchemaVersion)
				.Subject.Value.Should().BeOfType<string>()
				.Subject.Should().Be(OpenTelemetry.OpenTelemetrySchemaVersion);

#if NET8_0_OR_GREATER
			activity.Status.Should().Be(ActivityStatusCode.Ok);
#endif
		}
	}

	[Fact]
	public async Task PreferSpanNameFromOpenTelemetryData()
	{
		const string spanName = "Overridden span name";

		await TestCoreAsync(Assertions, static a => a.DisplayName = spanName);

		static void Assertions(Activity activity) => _ = activity.DisplayName.Should().Be(spanName);
	}

	[Fact]
	public async Task IncludeAttributesFromOpenTelemetryData()
	{
		const string attributeName = "test.attribute";
		const string attributeValue = "test-value";

		await TestCoreAsync(Assertions, static a => a.AddTag(attributeName, attributeValue));

		static void Assertions(Activity activity) => _ = activity.TagObjects.Should().Contain(t => t.Key == attributeName && (string)t.Value == attributeValue);
	}

	private static Task TestCoreAsync(Action<Activity> assertion) => TestCoreAsync(assertion, default);

	private static async Task TestCoreAsync(Action<Activity> assertions, Action<Activity> configureActivity, ITransport transport = null)
	{
		var mre = new ManualResetEvent(false);

		var callCounter = 0;
		using var listener = new ActivityListener
		{
			ActivityStarted = _ => { },
			ActivityStopped = activity =>
			{
				callCounter++;

				if (callCounter > 1)
					Assert.Fail("Expected one activity, but received multiple stop events.");

				assertions(activity);
				_ = mre.Set();
			},
			ShouldListenTo = activitySource => activitySource.Name == OpenTelemetry.ElasticTransportActivitySourceName,
			Sample = (ref _) => ActivitySamplingResult.AllData
		};
		ActivitySource.AddActivityListener(listener);

		transport ??= new DistributedTransport(InMemoryConnectionFactory.Create());

		_ = await transport.RequestAsync<VoidResponse>(new EndpointPath(HttpMethod.GET, "/"), null, configureActivity, null, default);

		_ = mre.WaitOne(TimeSpan.FromSeconds(1)).Should().BeTrue();
	}
}

[CollectionDefinition(nameof(NonParallelCollection), DisableParallelization = true)]
#pragma warning disable CA1711
public class NonParallelCollection { }
#pragma warning restore CA1711
