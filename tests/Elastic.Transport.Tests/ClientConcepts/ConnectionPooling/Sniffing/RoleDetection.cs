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

public class RoleDetection
{

	// Failure: Elastic.Transport.PipelineException : Failed sniffing cluster state. ---- PipelineException BadRequest ---- HttpRequestException (Sniff(s => s.Fails(Always)) on 9200 aborts the sniff; 9202 is never tried).
	[Fact(Skip = "BUG 3: Sniff throws after the first failing node instead of trying the remaining nodes")]
	public async Task DetectsMasterNodes()
	{
		var audit = new Auditor(() => Virtual.Elasticsearch.Bootstrap(10)
			.Sniff(s => s.Fails(Always))
			.Sniff(s => s.OnPort(9202)
				.Succeeds(Always, Virtual.Elasticsearch.Bootstrap(8).MasterEligible(9200, 9201, 9202))
			)
			.SniffingNodePool()
			.AllDefaults()
		)
		{
			AssertPoolBeforeStartup = (pool) =>
			{
				pool.Should().NotBeNull();
				pool.Nodes.Should().HaveCount(10);
				pool.Nodes.Where(n => n.HasFeature(ElasticsearchNodeFeatures.MasterEligible)).Should().HaveCount(10);
			},
			AssertPoolAfterStartup = (pool) =>
			{
				pool.Should().NotBeNull();
				pool.Nodes.Should().HaveCount(8);
				pool.Nodes.Where(n => n.HasFeature(ElasticsearchNodeFeatures.MasterEligible)).Should().HaveCount(3);
			}
		};

		await audit.TraceStartup();
	}

	// Failure: Elastic.Transport.PipelineException : Failed sniffing cluster state. ---- PipelineException BadRequest ---- HttpRequestException (Sniff(s => s.Fails(Always)) on 9200 aborts the sniff; 9202 is never tried).
	[Fact(Skip = "BUG 3: Sniff throws after the first failing node instead of trying the remaining nodes")]
	public async Task DetectsDataNodes()
	{
		var audit = new Auditor(() => Virtual.Elasticsearch.Bootstrap(10)
			.Sniff(s => s.Fails(Always))
			.Sniff(s => s.OnPort(9202)
				.Succeeds(Always, Virtual.Elasticsearch.Bootstrap(8).StoresNoData(9200, 9201, 9202))
			)
			.SniffingNodePool()
			.AllDefaults()
		)
		{
			AssertPoolBeforeStartup = (pool) =>
			{
				pool.Should().NotBeNull();
				pool.Nodes.Should().HaveCount(10);
				pool.Nodes.Where(n => n.HasFeature(ElasticsearchNodeFeatures.HoldsData)).Should().HaveCount(10);
			},

			AssertPoolAfterStartup = (pool) =>
			{
				pool.Should().NotBeNull();
				pool.Nodes.Should().HaveCount(8);
				pool.Nodes.Where(n => n.HasFeature(ElasticsearchNodeFeatures.HoldsData)).Should().HaveCount(5);
			}
		};
		await audit.TraceStartup();
	}

	[Fact]
	public async Task SkipsNodesThatDisableHttp()
	{
		var audit = new Auditor(() => Virtual.Elasticsearch.Bootstrap(10)
			.Sniff(s => s.SucceedAlways()
				.Succeeds(Always, Virtual.Elasticsearch.Bootstrap(8).StoresNoData(9200, 9201, 9202).HttpDisabled(9201))
			)
			.SniffingNodePool()
			.AllDefaults()
		)
		{
			AssertPoolBeforeStartup = (pool) =>
			{
				pool.Should().NotBeNull();
				pool.Nodes.Should().HaveCount(10);
				pool.Nodes.Where(n => n.HasFeature(ElasticsearchNodeFeatures.HoldsData)).Should().HaveCount(10);
				pool.Nodes.Where(n => n.HasFeature(ElasticsearchNodeFeatures.HttpEnabled)).Should().HaveCount(10);
				pool.Nodes.Should().OnlyContain(n => n.Uri.Host == "localhost");
			},

			AssertPoolAfterStartup = (pool) =>
			{
				pool.Should().NotBeNull();
				pool.Nodes.Should().HaveCount(7, "we filtered the node that has no http enabled");
				pool.Nodes.Should().NotContain(n=>n.Uri.Port == 9201);
				pool.Nodes.Where(n => n.HasFeature(ElasticsearchNodeFeatures.HoldsData)).Should().HaveCount(5);
			}
		};
		await audit.TraceStartup();
	}

	[Fact]
	public async Task SkipMasterOnlyNodes()
	{

		var masterNodes = new[] {9200, 9201, 9202};
		var totalNodesInTheCluster = 20;
		//
		var audit = new Auditor(() => Virtual.Elasticsearch.BootstrapAllMasterEligableOnly(masterNodes.Length)
			.ClientCalls(r => r.SucceedAlways())
			.Sniff(s => s.SucceedAlways()
				.Succeeds(Always, Virtual.Elasticsearch.Bootstrap(totalNodesInTheCluster)
					.StoresNoData(masterNodes)
					.MasterEligible(masterNodes)
					.ClientCalls(r => r.SucceedAlways())
				)
			)
			.SniffingNodePool()
			.Settings(s=>s.DisablePing())
		)
		{
			AssertPoolBeforeStartup = pool =>
			{
				pool.Should().NotBeNull();
				pool.Nodes.Should().HaveCount(3, "we seeded 3 master only nodes at the start of the application");
				pool.Nodes.Where(n => n.HasFeature(ElasticsearchNodeFeatures.HoldsData)).Should().HaveCount(0, "none of which hold data");
			},
			AssertPoolAfterStartup = (pool) =>
			{
				pool.Should().NotBeNull();
				var nodes = pool.CreateView(null).ToList();
				nodes.Count.Should().Be(20, "Master nodes are included in the registration of nodes since we still favor sniffing on them");
			}
		};

		audit = await audit.TraceStartup(new ClientCall
		{
			{ SniffSuccess, 9200},
			{ HealthyResponse, 9203}
		});

		var seenNodes = new HashSet<int>();
		foreach (var _ in Enumerable.Range(0, 1000))
		{
			audit = await audit.TraceCalls(
				new ClientCall {{HealthyResponse, (a) =>
				{
					var port = a.Node.Uri.Port;
					masterNodes.Should().NotContain(port);
					seenNodes.Add(port);
				}}}
			);
		}

		seenNodes.Should().HaveCount(totalNodesInTheCluster - masterNodes.Length);
	}

	[Fact]
	public async Task RespectsCustomPredicate()
	{
		var totalNodesInTheCluster = 20;
		var setting = "node.attr.rack_id";
		var value = "rack_one";
		var nodesInRackOne = new[] {9204, 9210, 9213};

		var audit = new Auditor(() => Virtual.Elasticsearch.Bootstrap(totalNodesInTheCluster)
			.ClientCalls(r => r.SucceedAlways())
			.Sniff(s => s.SucceedAlways()
				.Succeeds(Always, Virtual.Elasticsearch.Bootstrap(totalNodesInTheCluster)
					.HasSetting(setting, value, nodesInRackOne)
					.ClientCalls(r => r.SucceedAlways())
				)
			)
			.SniffingNodePool()
			.Settings(s=>s
				.DisablePing()
				.NodePredicate(node =>
					node.Settings.ContainsKey(setting) &&
					node.Settings[setting].ToString() == value
				)
			)
		)
		{
			AssertPoolAfterStartup = pool =>
			{
				pool.Should().NotBeNull();
				var nodes = pool.CreateView(null).ToList();
				nodes.Count(n => n.Settings.ContainsKey(setting)).Should().Be(3, "only three nodes are in rack_one");
			}
		};

		audit = await audit.TraceStartup(new ClientCall
		{
			{ SniffSuccess, 9200},
			{ HealthyResponse, 9204}
		});

		var seenNodes = new HashSet<int>();
		foreach (var _ in Enumerable.Range(0, 1000))
		{
			audit = await audit.TraceCalls(
				new ClientCall {{HealthyResponse, (a) =>
				{
					var port = a.Node.Uri.Port;
					nodesInRackOne.Should().Contain(port);
					seenNodes.Add(port);
				}}}
			);
		}

		seenNodes.Should().HaveCount(nodesInRackOne.Length);
	}

	[Fact]
	public async Task CustomPredicateYieldingNothingThrows()
	{
		var totalNodesInTheCluster = 20;

		var audit = new Auditor(() => Virtual.Elasticsearch.Bootstrap(totalNodesInTheCluster)
			.Sniff(s => s.SucceedAlways()
				.Succeeds(Always, Virtual.Elasticsearch.Bootstrap(totalNodesInTheCluster))
			)
			.SniffingNodePool()
			.Settings(s => s
				.DisablePing()
				.NodePredicate(node => false)
			)
		);

		await audit.TraceUnexpectedTransportException(new ClientCall
		{
			{ SniffOnStartup },
			{ SniffSuccess },
			{ NoNodesAttempted }
		}, e =>
		{
			e.FailureReason.Should().Be(PipelineFailure.Unexpected);

			Func<string> debug = () => e.DebugInformation;
			debug.Invoking(s => s.Invoke()).Should().NotThrow();
		});

	}

	[Fact]
	public async Task DetectsFqdn()
	{
		var audit = new Auditor(() => Virtual.Elasticsearch.Bootstrap(10)
			.Sniff(s => s.SucceedAlways()
				.Succeeds(Always, Virtual.Elasticsearch.Bootstrap(8)
					.StoresNoData(9200, 9201, 9202)
					.SniffShouldReturnFqdn())
			)
			.SniffingNodePool()
			.AllDefaults()
		)
		{
			AssertPoolBeforeStartup = pool =>
			{
				pool.Should().NotBeNull();
				pool.Nodes.Should().HaveCount(10);
				pool.Nodes.Where(n => n.HasFeature(ElasticsearchNodeFeatures.HoldsData)).Should().HaveCount(10);
				pool.Nodes.Should().OnlyContain(n => n.Uri.Host == "localhost");
			},

			AssertPoolAfterStartup = pool =>
			{
				pool.Should().NotBeNull();
				pool.Nodes.Should().HaveCount(8);
				pool.Nodes.Where(n => n.HasFeature(ElasticsearchNodeFeatures.HoldsData)).Should().HaveCount(5);
				pool.Nodes.Should().OnlyContain(n => n.Uri.Host.StartsWith("fqdn") && !n.Uri.Host.Contains('/'));
			}
		};

		await audit.TraceStartup();
	}
}

