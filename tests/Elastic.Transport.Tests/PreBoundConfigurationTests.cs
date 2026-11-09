// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Linq;
using Elastic.Transport.Diagnostics.Auditing;
using Elastic.Transport.VirtualizedCluster;
using FluentAssertions;
using Xunit;

namespace Elastic.Transport.Tests;

/// <summary>
/// <see cref="BoundConfiguration.Create"/> exists so a request configuration can be resolved once and reused, instead of
/// merged with the transport's on every call. That only works if the result is accepted back as a request's configuration.
/// </summary>
public class PreBoundConfigurationTests
{
	[Fact]
	public void ABoundConfigurationIsARequestConfiguration() =>
		_ = typeof(IRequestConfiguration).IsAssignableFrom(typeof(BoundConfiguration)).Should().BeTrue();

	[Fact]
	public void APreBoundConfigurationIsHonouredOnEveryCall()
	{
		var cluster = Virtual.Elasticsearch.Bootstrap(3)
			.ClientCalls(r => r.FailAlways())
			.StaticNodePool()
			.Settings(s => s.DisablePing().ThrowExceptions(false));

		// Every node fails, so without the override a call would try all three. With MaxRetries = 0 it tries one.
		var bound = BoundConfiguration.Create(cluster.RequestHandler, new RequestConfiguration { MaxRetries = 0 });

		for (var i = 0; i < 3; i++)
		{
			var response = cluster.ClientCall(_ => bound);

			_ = response.ApiCallDetails.HasSuccessfulStatusCode.Should().BeFalse($"call {i}");
			_ = response.ApiCallDetails.AuditTrail!
				.Count(a => a.Event is AuditEvent.BadRequest or AuditEvent.BadResponse or AuditEvent.HealthyResponse)
				.Should().Be(1, $"call {i}");
		}
	}
}
