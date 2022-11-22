// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Diagnostics;
using System.Threading.Tasks;
using Elastic.Transport.Diagnostics.Auditing;
using Elastic.Transport.VirtualizedCluster;
using Elastic.Transport.VirtualizedCluster.Audit;
using Elastic.Transport.VirtualizedCluster.Rules;
using FluentAssertions;
using Xunit;

namespace Elastic.Transport.Tests;

public class ActivityTest
{
	[Fact]
	public async Task BasicOpenTelemetryTest()
	{
		Activity oTelActivity = null;
		var listener = new ActivityListener
		{
			ActivityStarted = _ => { },
			ActivityStopped = activity => oTelActivity = activity,
			ShouldListenTo = activitySource => activitySource.Name == "Elastic.Transport.RequestPipeline",
			Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
		};
		ActivitySource.AddActivityListener(listener);

		var audit =
			new Auditor(() => Virtual.Elasticsearch
				.Bootstrap(1)
				.ClientCalls(r => r.Succeeds(TimesHelper.Once).ReturnResponse(new { x = 1 }))
				.StaticNodePool()
				.Settings(s => s.DisablePing().EnableDebugMode())
			);
		_ = await audit.TraceCalls(
			new ClientCall { { AuditEvent.HealthyResponse, 9200, response => { } }, }
		);

		oTelActivity.Should().NotBeNull();

		oTelActivity.Kind.Should().Be(ActivityKind.Client);

		oTelActivity.DisplayName.Should().Be("Elastic.Transport: HTTP GET");
		oTelActivity.OperationName.Should().Be("Elastic.Transport: HTTP GET");

		oTelActivity.Tags.Should().Contain(n => n.Key == "http.url" && n.Value == "http://localhost:9200/");
		oTelActivity.Tags.Should().Contain(n => n.Key == "net.peer.name" && n.Value == "localhost");

		oTelActivity.Status.Should().Be(ActivityStatusCode.Ok);
	}
}
