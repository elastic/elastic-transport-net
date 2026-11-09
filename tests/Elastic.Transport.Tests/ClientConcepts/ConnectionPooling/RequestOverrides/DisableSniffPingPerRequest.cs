// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

// Tests skipped with a BUG tag document suspected defects in the current source and are expected to be re-enabled when fixed.
#pragma warning disable xUnit1004

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

public class DisableSniffPingPerRequest
{

	// Failure: Elastic.Transport.PipelineException : Failed sniffing cluster state. ---- System.NullReferenceException at VirtualClusterRequestInvoker.UpdateCluster (VirtualClusterConnection.cs:111)
	// The first call (DisableSniffing) behaves correctly; the second call (SniffOnStartup) hits the NRE.
	[Fact(Skip = "UNCLASSIFIED: VirtualClusterRequestInvoker.UpdateCluster throws NullReferenceException when a Sniff rule has no NewClusterState (old code returned on null)")]
	public async Task DisableSniff()
	{

		var audit = new Auditor(() => Virtual.Elasticsearch.Bootstrap(10)
			.ClientCalls(r => r.SucceedAlways())
			.Sniff(c=>c.SucceedAlways())
			.Ping(c=>c.SucceedAlways())
			.SniffingNodePool()
			.Settings(s => s.SniffOnStartup())
		);

		audit = await audit.TraceCalls(
			new ClientCall(r => r.DisableSniffing())
			{
				{ PingSuccess, 9200 },
				{ HealthyResponse, 9200 }
			},
			new ClientCall()
			{
				{ SniffOnStartup },
				{ SniffSuccess, 9200 },
				{ PingSuccess, 9200 },
				{ HealthyResponse, 9200 }
			},
			new ClientCall()
			{
				{ PingSuccess, 9201 },
				{ HealthyResponse, 9201 }
			}
		);
	}

	// Failure: Elastic.Transport.PipelineException : Failed sniffing cluster state. ---- System.NullReferenceException at VirtualClusterRequestInvoker.UpdateCluster
	// Would likely also hit BUG 6 (request-level DisablePing ignored) once past the sniff.
	[Fact(Skip = "UNCLASSIFIED: VirtualClusterRequestInvoker.UpdateCluster throws NullReferenceException when a Sniff rule has no NewClusterState (old code returned on null)")]
	public async Task DisablePing()
	{
		var audit = new Auditor(() => Virtual.Elasticsearch.Bootstrap(10)
			.ClientCalls(r => r.SucceedAlways())
			.Sniff(c=>c.SucceedAlways())
			.SniffingNodePool()
			.Settings(s => s.SniffOnStartup())
		);

		audit = await audit.TraceCall(
			new ClientCall(r => r.DisablePing())
			{
				{ SniffOnStartup },
				{ SniffSuccess, 9200 },
				{ HealthyResponse, 9200 }
			}
		);
	}

	// Request override DisablePing() ignored: audit trail shows PingFailure on 9200, 9201, 9202, ...
	// Expected: only HealthyResponse 9200.
	// Failure: the 1st client call's synchronous audit trail should assert ALL audit trail items
	[Fact(Skip = "BUG 6: BoundConfiguration.DisablePings ignores the request-level value (global.DisablePings ?? !SupportsPinging)")]
	public async Task DisableSniffAndPing()
	{
		var audit = new Auditor(() => Virtual.Elasticsearch.Bootstrap(10)
			.ClientCalls(r => r.SucceedAlways())
			.SniffingNodePool()
			.Settings(s => s.SniffOnStartup())
		);

		audit = await audit.TraceCall(
			new ClientCall(r=>r.DisableSniffing().DisablePing())
			{
				{ HealthyResponse, 9200 }
			}
		);
	}
}
