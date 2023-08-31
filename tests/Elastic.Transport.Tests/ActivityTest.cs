// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Elastic.Transport.Diagnostics;
using Elastic.Transport.Tests.Plumbing;
using FluentAssertions;
using Xunit;

namespace Elastic.Transport.Tests;

// We cannot allow these tests to run in parellel with other tests as the listener may pick up other activities.
[Collection(nameof(NonParallelCollection))]
public class ActivityTests
{
	[Fact]
	public async Task BasicOpenTelemetryTest()
	{
		await ExecuteTestAsync(Assertions);

		static void Assertions(Activity activity)
		{
			activity.Should().NotBeNull();
			activity.Kind.Should().Be(ActivityKind.Client);
			activity.DisplayName.Should().Be("GET");
			activity.OperationName.Should().Be("GET");
			activity.TagObjects.Should().Contain(t => t.Key == SemanticConventions.UrlFull && (string)t.Value == "http://localhost:9200/");
			activity.TagObjects.Should().Contain(n => n.Key == SemanticConventions.ServerAddress && (string)n.Value == "localhost");
#if !NETFRAMEWORK
			activity.Status.Should().Be(ActivityStatusCode.Ok);
#endif

			// TODO - Other assertions
		}
	}

	[Fact]
	public async Task PreferSpanNameFromOpenTelemetryData()
	{
		const string spanName = "Overridden span name";

		await ExecuteTestAsync(new OpenTelemetryData { SpanName = spanName }, Assertions);

		static void Assertions(Activity activity)
		{
			activity.DisplayName.Should().Be(spanName);
		}
	}

	[Fact]
	public async Task IncludeAttributesFromOpenTelemetryData()
	{
		const string attributeName = "test.attribute";
		const string attributeValue = "test-value";

		await ExecuteTestAsync(new OpenTelemetryData
		{
			SpanAttributes = new System.Collections.Generic.Dictionary<string, object>
			{
				[attributeName] = attributeValue
			}
		}, Assertions);

		static void Assertions(Activity activity)
		{
			activity.TagObjects.Should().Contain(t => t.Key == attributeName && (string)t.Value == attributeValue);
		}
	}

	private Task ExecuteTestAsync(Action<Activity> assertion) => ExecuteTestAsync(default, assertion);

	private async Task ExecuteTestAsync(OpenTelemetryData openTelemetryData, Action<Activity> assertions)
	{
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

				assertions(activity);
				mre.Set();
			},
			ShouldListenTo = activitySource => activitySource.Name == OpenTelemetry.ElasticTransportActivitySourceName,
			Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
		};
		ActivitySource.AddActivityListener(listener);

		var transport = new DefaultHttpTransport(InMemoryConnectionFactory.Create());
		_ = await transport.RequestAsync<VoidResponse>(HttpMethod.GET, "/", null, null, openTelemetryData);

		mre.WaitOne(TimeSpan.FromSeconds(1)).Should().BeTrue();
	}
}

[CollectionDefinition(nameof(NonParallelCollection), DisableParallelization = true)]
public class NonParallelCollection { }
