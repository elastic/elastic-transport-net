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
using Elastic.Transport.Products.Elasticsearch;
using Elastic.Transport.VirtualizedCluster;
using Elastic.Transport.VirtualizedCluster.Audit;
using Elastic.Transport.VirtualizedCluster.Providers;
using Elastic.Transport.VirtualizedCluster.Rules;
using FluentAssertions;
using Xunit;
using static Elastic.Transport.Diagnostics.Auditing.AuditEvent;
using static Elastic.Transport.VirtualizedCluster.Rules.TimesHelper;

namespace Elastic.Transport.Tests.ClientConcepts.ConnectionPooling.Sniffing;

public class OnStartupSniffing
{

	// Failure: Elastic.Transport.PipelineException : Failed sniffing cluster state. ---- PipelineException BadRequest ---- HttpRequestException (first node's sniff failure; the remaining nodes are never tried).
	[Fact(Skip = "BUG 3: Sniff throws after the first failing node instead of trying the remaining nodes")]
	public async Task ASniffOnStartupHappens()
	{

		var audit = new Auditor(() => Virtual.Elasticsearch.Bootstrap(10)
			.Sniff(s => s.Fails(Always))
			.Sniff(s => s.OnPort(9202).Succeeds(Always))
			.Ping(c=>c.SucceedAlways())
			.ClientCalls(r => r.SucceedAlways())
			.SniffingNodePool()
			.AllDefaults()
		);

		 await audit.TraceCall(
			 new ClientCall
			 {
				{ SniffOnStartup},
				{ SniffFailure, 9200},
				{ SniffFailure, 9201},
				{ SniffSuccess, 9202},
				{ PingSuccess , 9200},
				{ HealthyResponse, 9200}
			}
		 );
	}

	// Failure: Elastic.Transport.PipelineException : Failed sniffing cluster state. ---- PipelineException BadRequest ---- HttpRequestException (first node's sniff failure; the remaining nodes are never tried).
	[Fact(Skip = "BUG 3: Sniff throws after the first failing node instead of trying the remaining nodes")]
	public async Task ASniffOnStartupHappensOnce()
	{
		var audit = new Auditor(() => Virtual.Elasticsearch.Bootstrap(10)
			.Sniff(s => s.Fails(Always))
			.Sniff(s => s.OnPort(9202).Succeeds(Always))
			.ClientCalls(r => r.SucceedAlways())
			.Ping(c=>c.SucceedAlways())
			.SniffingNodePool()
			.AllDefaults()
		);

		 await audit.TraceCalls(
			 new ClientCall
			 {
				{ SniffOnStartup},
				{ SniffFailure, 9200},
				{ SniffFailure, 9201},
				{ SniffSuccess, 9202},
				{ PingSuccess , 9200},
				{ HealthyResponse, 9200}
			},
			new ClientCall
			{
				{ PingSuccess, 9201},
				{ HealthyResponse, 9201}
			}
		);
	}

	// Failure: Elastic.Transport.PipelineException : Failed sniffing cluster state. ---- PipelineException BadRequest ---- HttpRequestException (first node's sniff failure; the remaining nodes are never tried).
	[Fact(Skip = "BUG 3: Sniff throws after the first failing node instead of trying the remaining nodes")]
	public async Task SniffOnStartUpTakesNewClusterState()
	{
		var audit = new Auditor(() => Virtual.Elasticsearch.Bootstrap(10)
			.Sniff(s => s.Fails(Always))
			.Sniff(s => s.OnPort(9202).Succeeds(Always,
				Virtual.Elasticsearch.Bootstrap(8, startFrom: 9204)
					.Ping(c=>c.SucceedAlways())
					.ClientCalls(c=>c.SucceedAlways())

				))
			.ClientCalls(r => r.SucceedAlways())
			.Ping(c=>c.SucceedAlways())
			.SniffingNodePool()
			.AllDefaults()
		);

		await audit.TraceCall(new ClientCall {
			{ SniffOnStartup},
			{ SniffFailure, 9200},
			{ SniffFailure, 9201},
			{ SniffSuccess, 9202},
			{ PingSuccess, 9204},
			{ HealthyResponse, 9204}
		});
	}

	// Failure: Elastic.Transport.PipelineException : Failed sniffing cluster state. ---- PipelineException BadRequest ---- HttpRequestException (first node's sniff failure; the remaining nodes are never tried).
	[Fact(Skip = "BUG 3: Sniff throws after the first failing node instead of trying the remaining nodes")]
	public async Task SniffTriesAllNodes()
	{
		var audit = new Auditor(() => Virtual.Elasticsearch.Bootstrap(10)
			.Sniff(s => s.Fails(Always))
			.Sniff(s => s.OnPort(9209).Succeeds(Always))
			.Ping(c=>c.SucceedAlways())
			.ClientCalls(r => r.SucceedAlways())
			.SniffingNodePool()
			.AllDefaults()
		);

		await audit.TraceCall(new ClientCall {
			{ SniffOnStartup},
			{ SniffFailure, 9200},
			{ SniffFailure, 9201},
			{ SniffFailure, 9202},
			{ SniffFailure, 9203},
			{ SniffFailure, 9204},
			{ SniffFailure, 9205},
			{ SniffFailure, 9206},
			{ SniffFailure, 9207},
			{ SniffFailure, 9208},
			{ SniffSuccess, 9209},
			{ PingSuccess, 9200},
			{ HealthyResponse, 9200}
		});
	}

	// Failure: Elastic.Transport.PipelineException : Failed sniffing cluster state. ---- System.NullReferenceException at VirtualClusterRequestInvoker.UpdateCluster (Sniff(s => s.Succeeds(Always)) without a cluster).
	[Fact(Skip = "UNCLASSIFIED: VirtualClusterRequestInvoker.UpdateCluster throws NullReferenceException when a Sniff rule has no NewClusterState (old code returned on null)")]
	public async Task SniffPrefersMasterNodes()
	{
		var audit = new Auditor(() => Virtual.Elasticsearch.Bootstrap(new[] {
				new Node(new Uri("http://localhost:9200"), ElasticsearchNodeFeatures.Default.Except([ElasticsearchNodeFeatures.MasterEligible])),
				new Node(new Uri("http://localhost:9201"), ElasticsearchNodeFeatures.Default.Except([ElasticsearchNodeFeatures.MasterEligible])),
				new Node(new Uri("http://localhost:9202"), ElasticsearchNodeFeatures.Default),
			})
			.Sniff(s => s.Succeeds(Always))
			.Ping(s => s.Succeeds(Always))
			.ClientCalls(r => r.SucceedAlways())
			.SniffingNodePool()
			.AllDefaults()
		);

		await audit.TraceCall(new ClientCall {
			{ SniffOnStartup},
			{ SniffSuccess, 9202},
			{ PingSuccess, 9200},
			{ HealthyResponse, 9200}
		});
	}

	// Failure: Elastic.Transport.PipelineException : Failed sniffing cluster state. ---- PipelineException BadRequest ---- HttpRequestException (first node's sniff failure; the remaining nodes are never tried).
	[Fact(Skip = "BUG 3: Sniff throws after the first failing node instead of trying the remaining nodes")]
	public async Task SniffPrefersMasterNodesButStillFailsOver()
	{
		var audit = new Auditor(() => Virtual.Elasticsearch.Bootstrap(new[] {
				new Node(new Uri("http://localhost:9200"), ElasticsearchNodeFeatures.Default),
				new Node(new Uri("http://localhost:9201"), ElasticsearchNodeFeatures.Default),
				new Node(new Uri("http://localhost:9202"), ElasticsearchNodeFeatures.Default.Except([ElasticsearchNodeFeatures.MasterEligible])),
			})
			.Sniff(s => s.Fails(Always))
			.Sniff(s => s.OnPort(9202).Succeeds(Always))
			.Ping(c=>c.SucceedAlways())
			.ClientCalls(r => r.SucceedAlways())
			.SniffingNodePool()
			.AllDefaults()
		);

		await audit.TraceCall(new ClientCall {
			{ SniffOnStartup},
			{ SniffFailure, 9200},
			{ SniffFailure, 9201},
			{ SniffSuccess, 9202},
			{ PingSuccess, 9200},
			{ HealthyResponse, 9200}
		});
	}
}
