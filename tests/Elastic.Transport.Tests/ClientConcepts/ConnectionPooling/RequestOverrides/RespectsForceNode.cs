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

public class RespectsForceNode
{

	[Fact]
	public async Task OnlyCallsForcedNode()
	{
		var audit = new Auditor(() => Virtual.Elasticsearch.Bootstrap(10)
			.ClientCalls(r => r.SucceedAlways())
			.ClientCalls(r => r.OnPort(9208).FailAlways())
			.StaticNodePool()
			.Settings(s => s.DisablePing())
		);

		audit = await audit.TraceCall(
			new ClientCall(r => r.ForceNode(new Uri("http://localhost:9208"))) {
				{ BadRequest, 9208 }
			}
		);
	}
}
