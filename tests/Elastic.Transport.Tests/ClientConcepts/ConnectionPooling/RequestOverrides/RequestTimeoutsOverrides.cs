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

public class RequestTimeoutsOverrides
{

	[Fact]
	public async Task RespectsRequestTimeoutOverride()
	{

		var audit = new Auditor(() => Virtual.Elasticsearch.Bootstrap(10)
			.ClientCalls(r => r.FailAlways().Takes(TimeSpan.FromSeconds(10)))
			.ClientCalls(r => r.OnPort(9209).SucceedAlways())
			.StaticNodePool()
			.Settings(s => s.DisablePing().RequestTimeout(TimeSpan.FromSeconds(20)))
		);

		audit = await audit.TraceCalls(
			new ClientCall {
				{ BadRequest, 9200 },
				{ BadRequest, 9201 },
				{ MaxTimeoutReached }
			},

			new ClientCall(r => r.RequestTimeout(TimeSpan.FromSeconds(80)))
			{
				{ BadRequest, 9203 },
				{ BadRequest, 9204 },
				{ BadRequest, 9205 },
				{ BadRequest, 9206 },
				{ BadRequest, 9207 },
				{ BadRequest, 9208 },
				{ HealthyResponse, 9209 },
			}
		);

	}

	[Fact]
	public async Task RespectsConnectTimeoutOverride()
	{

		var audit = new Auditor(() => Virtual.Elasticsearch.Bootstrap(10)
			.Ping(p => p.SucceedAlways().Takes(TimeSpan.FromSeconds(20)))
			.ClientCalls(r => r.SucceedAlways())
			.StaticNodePool()
			.Settings(s => s.RequestTimeout(TimeSpan.FromSeconds(10)).PingTimeout(TimeSpan.FromSeconds(10)))
		);

		audit = await audit.TraceCalls(

			new ClientCall {
				{ PingFailure, 9200 },
				{ MaxTimeoutReached }
			},

			new ClientCall(r => r.PingTimeout(TimeSpan.FromSeconds(2)))
			{
				{ PingFailure, 9202 },
				{ PingFailure, 9203 },
				{ PingFailure, 9204 },
				{ PingFailure, 9205 },
				{ PingFailure, 9206 },
				{ MaxTimeoutReached }
			}
		);

	}
}
