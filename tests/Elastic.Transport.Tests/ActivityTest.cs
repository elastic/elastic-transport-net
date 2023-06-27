// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Diagnostics;
using System.Threading.Tasks;
using Elastic.Transport.Tests.Plumbing;
using FluentAssertions;
using Xunit;

namespace Elastic.Transport.Tests;

// We cannot allow this test to run in parellel with other tests as the listener may pick up other activities
[Collection(nameof(NonParallelCollection))]
public class ActivityTest
{
	[Fact]
	public async Task BasicOpenTelemetryTest()
	{
		var callCounter = 0;
		var listener = new ActivityListener
		{
			ActivityStarted = _ => { },
			ActivityStopped = activity =>
			{
				callCounter++;

				if (callCounter > 1)
					Assert.Fail("Expected one activity, but received multiple stop events.");

				activity.Should().NotBeNull();
				activity.Kind.Should().Be(ActivityKind.Client);
				activity.DisplayName.Should().Be("Elastic.Transport: HTTP GET");
				activity.OperationName.Should().Be("Elastic.Transport: HTTP GET");
				activity.Tags.Should().Contain(n => n.Key == "http.url" && n.Value == "http://localhost:9200/");
				activity.Tags.Should().Contain(n => n.Key == "net.peer.name" && n.Value == "localhost");
#if !NETFRAMEWORK
				activity.Status.Should().Be(ActivityStatusCode.Ok);
#endif
			},
			ShouldListenTo = activitySource => activitySource.Name == "Elastic.Transport.RequestPipeline",
			Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
		};
		ActivitySource.AddActivityListener(listener);

		var transport = new DefaultHttpTransport(InMemoryConnectionFactory.Create());

		_ = await transport.RequestAsync<VoidResponse>(HttpMethod.GET, "/");
	}
}

[CollectionDefinition(nameof(NonParallelCollection), DisableParallelization = true)]
public class NonParallelCollection { }
