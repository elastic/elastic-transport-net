// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Linq;
using Elastic.Transport.Diagnostics.Auditing;
using Elastic.Transport.VirtualizedCluster;
using FluentAssertions;
using Xunit;

namespace Elastic.Transport.Tests;

public class AuditRecordTests
{
	[Fact]
	public void AnAuditTrailEntryHasValueEqualityAndKeepsItsCustomToString()
	{
		var response = Virtual.Elasticsearch.Bootstrap(1)
			.ClientCalls(r => r.SucceedAlways())
			.StaticNodePool()
			.Settings(s => s.DisablePing())
			.ClientCall();

		var audit = response.ApiCallDetails.AuditTrail!.Single();

		var copy = audit with { };
		_ = copy.Should().Be(audit);
		_ = copy.Should().NotBeSameAs(audit);
		_ = audit.ToString().Should().StartWith("Event: HealthyResponse");
	}
}
