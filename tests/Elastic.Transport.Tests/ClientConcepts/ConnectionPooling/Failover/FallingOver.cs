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

namespace Elastic.Transport.Tests.ClientConcepts.ConnectionPooling.Failover;

public class FallingOver
{

	[Fact]
	public async Task ExceptionFallsOverToNextNode()
	{
		var audit = new Auditor(() => Virtual.Elasticsearch.Bootstrap(10)
			.ClientCalls(r => r.FailAlways())
			.ClientCalls(r => r.OnPort(9201).SucceedAlways())
			.StaticNodePool()
			.Settings(s => s.DisablePing())
		);

		audit = await audit.TraceCall(
			new ClientCall {
				{ BadRequest, 9200 },
				{ HealthyResponse, 9201 },
			}
		);
	}

	[Fact]
	public async Task Http502FallsOver()
	{
		var audit = new Auditor(() => Virtual.Elasticsearch.Bootstrap(10)
			.ClientCalls(r => r.FailAlways(502))
			.ClientCalls(r => r.OnPort(9201).SucceedAlways())
			.StaticNodePool()
			.Settings(s => s.DisablePing())
		);

		audit = await audit.TraceCall(
			new ClientCall {
				{ BadResponse, 9200 },
				{ HealthyResponse, 9201 },
			}
		);
	}

	[Fact]
	public async Task Http503FallsOver()
	{
		var audit = new Auditor(() => Virtual.Elasticsearch.Bootstrap(10)
			.ClientCalls(r => r.FailAlways(503))
			.ClientCalls(r => r.OnPort(9201).SucceedAlways())
			.StaticNodePool()
			.Settings(s => s.DisablePing())
		);

		audit = await audit.TraceCall(
			new ClientCall {
				{ BadResponse, 9200 },
				{ HealthyResponse, 9201 },
			}
		);
	}

	[Fact]
	public async Task Http504FallsOver()
	{
		var audit = new Auditor(() => Virtual.Elasticsearch.Bootstrap(10)
			.ClientCalls(r => r.FailAlways(504))
			.ClientCalls(r => r.OnPort(9201).SucceedAlways())
			.StaticNodePool()
			.Settings(s => s.DisablePing())
		);

		audit = await audit.TraceCall(
			new ClientCall {
				{ BadResponse, 9200 },
				{ HealthyResponse, 9201 },
			}
		);
	}

	[Fact]
	public async Task HttpTeapotDoesNotFallOver()
	{
		var audit = new Auditor(() => Virtual.Elasticsearch.Bootstrap(10)
			.ClientCalls(r => r.FailAlways(418))
			.ClientCalls(r => r.OnPort(9201).SucceedAlways())
			.StaticNodePool()
			.Settings(s => s.DisablePing())
		);

		audit = await audit.TraceCall(
			new ClientCall {
				{ BadResponse, 9200 },
			}
		);
	}
}
