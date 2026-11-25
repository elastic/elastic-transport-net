// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Elastic.Transport.VirtualizedCluster;
using Elastic.Transport.VirtualizedCluster.Audit;
using Elastic.Transport.VirtualizedCluster.Providers;
using Elastic.Transport.VirtualizedCluster.Rules;
using FluentAssertions;
using Xunit;
using static Elastic.Transport.Diagnostics.Auditing.AuditEvent;
using static Elastic.Transport.VirtualizedCluster.Rules.TimesHelper;

namespace Elastic.Transport.Tests.ClientConcepts.ConnectionPooling.RequestOverrides;

public class RespectsAllowedStatusCode
{

	[Fact]
	public async Task CanOverrideBadResponse()
	{
		var audit = new Auditor(() => Virtual.Elasticsearch.Bootstrap(10)
			.ClientCalls(r => r.FailAlways(400))
			.StaticNodePool()
			.Settings(s => s.DisablePing().MaximumRetries(0))
		);

		audit = await audit.TraceCalls(
			new ClientCall {
				{ BadResponse, 9200 }
			},
			new ClientCall(r => r.AllowedStatusCodes(400)) {
				{ HealthyResponse, 9201 }
			}
		);
	}
}
