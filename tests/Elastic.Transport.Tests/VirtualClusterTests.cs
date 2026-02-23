// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Linq;
using System.Threading.Tasks;
using Elastic.Transport.VirtualizedCluster;
using Elastic.Transport.VirtualizedCluster.Audit;
using Elastic.Transport.VirtualizedCluster.Rules;
using FluentAssertions;
using Xunit;
using static Elastic.Transport.Diagnostics.Auditing.AuditEvent;

namespace Elastic.Transport.Tests;

public class VirtualClusterTests
{
	[Fact]
	public async Task ThrowsExceptionWithNoRules()
	{
		var audit = new Auditor(() => Virtual.Elasticsearch
			.Bootstrap(1)
			.StaticNodePool()
			.Settings(s => s.DisablePing().EnableDebugMode())
		);
		var e = await Assert.ThrowsAsync<UnexpectedTransportException>(
			async () => await audit.TraceCalls(new ClientCall()));

		_ = e.Message.Should().Contain("No ClientCalls defined for the current VirtualCluster, so we do not know how to respond");
	}

	[Fact]
	public async Task ThrowsExceptionAfterDepleedingRules()
	{
		var audit = new Auditor(() => Virtual.Elasticsearch
			.Bootstrap(1)
			.ClientCalls(r => r.Succeeds(TimesHelper.Once).ReturnResponse(new { x = 1 }))
			.StaticNodePool()
			.Settings(s => s.DisablePing().EnableDebugMode())
		);
		audit = await audit.TraceCalls(
			new ClientCall {

				{ HealthyResponse, 9200, response =>
				{
					_ = response.ApiCallDetails.HasSuccessfulStatusCode.Should().BeTrue();
					_ = response.ApiCallDetails.HttpStatusCode.Should().Be(200);
					_ = response.ApiCallDetails.DebugInformation.Should().Contain("x\":1");
				} },
			}
		);
		var e = await Assert.ThrowsAsync<UnexpectedTransportException>(
			async () => await audit.TraceCalls(new ClientCall()));

		_ = e.Message.Should().Contain("No global or port specific ClientCalls rule (9200) matches any longer after 2 calls in to the cluster");
	}

	[Fact]
	public async Task AGlobalRuleStaysValidForever()
	{
		var audit = new Auditor(() => Virtual.Elasticsearch
			.Bootstrap(1)
			.ClientCalls(c => c.SucceedAlways())
			.StaticNodePool()
			.Settings(s => s.DisablePing())
		);

		_ = await audit.TraceCalls(
			Enumerable.Range(0, 1000)
				.Select(i => new ClientCall { { HealthyResponse, 9200 }, })
				.ToArray()
		);

	}

	[Fact]
	public async Task RulesAreIgnoredAfterBeingExecuted()
	{
		var audit = new Auditor(() => Virtual.Elasticsearch
			.Bootstrap(1)
			.ClientCalls(r => r.Succeeds(TimesHelper.Once).ReturnResponse(new { x = 1 }))
			.ClientCalls(r => r.Fails(TimesHelper.Once, 500).ReturnResponse(new { x = 2 }))
			.ClientCalls(r => r.Fails(TimesHelper.Twice, 400).ReturnResponse(new { x = 3 }))
			.ClientCalls(r => r.Succeeds(TimesHelper.Once).ReturnResponse(new { x = 4 }))
			.StaticNodePool()
			.Settings(s => s.DisablePing().EnableDebugMode())
		);
		_ = await audit.TraceCalls(
			new ClientCall {

				{ HealthyResponse, 9200, response =>
				{
					_ = response.ApiCallDetails.HasSuccessfulStatusCode.Should().BeTrue();
					_ = response.ApiCallDetails.HttpStatusCode.Should().Be(200);
					_ = response.ApiCallDetails.DebugInformation.Should().Contain("x\":1");
				} },
			},
			new ClientCall {

				{ BadResponse, 9200, response =>
				{
					_ = response.ApiCallDetails.HasSuccessfulStatusCode.Should().BeFalse();
					_ = response.ApiCallDetails.HttpStatusCode.Should().Be(500);
					_ = response.ApiCallDetails.DebugInformation.Should().Contain("x\":2");
				} },
			},
			new ClientCall {

				{ BadResponse, 9200, response =>
				{
					_ = response.ApiCallDetails.HttpStatusCode.Should().Be(400);
					_ = response.ApiCallDetails.DebugInformation.Should().Contain("x\":3");
				} },
			},
			new ClientCall {

				{ BadResponse, 9200, response =>
				{
					_ = response.ApiCallDetails.HttpStatusCode.Should().Be(400);
					_ = response.ApiCallDetails.DebugInformation.Should().Contain("x\":3");
				} },
			},
			new ClientCall {

				{ HealthyResponse, 9200, response =>
				{
					_ = response.ApiCallDetails.HttpStatusCode.Should().Be(200);
					_ = response.ApiCallDetails.DebugInformation.Should().Contain("x\":4");
				} },
			}
		);
	}

	[Fact]
	public async Task PathSpecificRulesReturnDifferentResponses()
	{
		var cluster = Virtual.Elasticsearch
			.Bootstrap(1)
			.ClientCalls(r => r.OnPath("/_search").SucceedAlways().ReturnResponse(new { endpoint = "search" }))
			.ClientCalls(r => r.OnPath("/_bulk").SucceedAlways().ReturnResponse(new { endpoint = "index" }))
			.ClientCalls(r => r.SucceedAlways().ReturnResponse(new { endpoint = "catchall" }))
			.StaticNodePool()
			.Settings(s => s.DisablePing().EnableDebugMode());

		var transport = cluster.RequestHandler;

		var searchResponse = transport.Request<StringResponse>(HttpMethod.GET, "/_search");
		_ = searchResponse.ApiCallDetails.HasSuccessfulStatusCode.Should().BeTrue();
		_ = searchResponse.Body.Should().Contain("endpoint\":\"search\"");

		var bulkResponse = transport.Request<StringResponse>(HttpMethod.POST, "/_bulk");
		_ = bulkResponse.ApiCallDetails.HasSuccessfulStatusCode.Should().BeTrue();
		_ = bulkResponse.Body.Should().Contain("endpoint\":\"index\"");

		var otherResponse = transport.Request<StringResponse>(HttpMethod.GET, "/other");
		_ = otherResponse.ApiCallDetails.HasSuccessfulStatusCode.Should().BeTrue();
		_ = otherResponse.Body.Should().Contain("endpoint\":\"catchall\"");
	}

	[Fact]
	public async Task PathSpecificRulesFallThroughToCatchAll()
	{
		var audit = new Auditor(() => Virtual.Elasticsearch
			.Bootstrap(1)
			.ClientCalls(r => r.OnPath("/_search").SucceedAlways().ReturnResponse(new { endpoint = "search" }))
			.ClientCalls(r => r.SucceedAlways().ReturnResponse(new { endpoint = "catchall" }))
			.StaticNodePool()
			.Settings(s => s.DisablePing().EnableDebugMode())
		);

		_ = await audit.TraceCalls(
			new ClientCall {
				{ HealthyResponse, 9200, response =>
				{
					_ = response.ApiCallDetails.HasSuccessfulStatusCode.Should().BeTrue();
					_ = response.ApiCallDetails.DebugInformation.Should().Contain("endpoint\":\"catchall\"");
				} },
			}
		);
	}

	[Fact]
	public void PathSpecificRulesWithTimesAreExhausted()
	{
		var cluster = Virtual.Elasticsearch
			.Bootstrap(1)
			.ClientCalls(r => r.OnPath("/_search").Succeeds(TimesHelper.Once).ReturnResponse(new { call = 1 }))
			.ClientCalls(r => r.OnPath("/_search").SucceedAlways().ReturnResponse(new { call = "default" }))
			.StaticNodePool()
			.Settings(s => s.DisablePing().EnableDebugMode());

		var transport = cluster.RequestHandler;

		var first = transport.Request<StringResponse>(HttpMethod.GET, "/_search");
		_ = first.Body.Should().Contain("call\":1");

		var second = transport.Request<StringResponse>(HttpMethod.GET, "/_search");
		_ = second.Body.Should().Contain("call\":\"default\"");
	}

	[Fact]
	public void PathSpecificRulesUsePredicateOverload()
	{
		var cluster = Virtual.Elasticsearch
			.Bootstrap(1)
			.ClientCalls(r => r.OnPath(p => p.StartsWith("/_cat", StringComparison.OrdinalIgnoreCase)).SucceedAlways().ReturnResponse(new { matched = "predicate" }))
			.ClientCalls(r => r.SucceedAlways().ReturnResponse(new { matched = "catchall" }))
			.StaticNodePool()
			.Settings(s => s.DisablePing().EnableDebugMode());

		var transport = cluster.RequestHandler;

		var catResponse = transport.Request<StringResponse>(HttpMethod.GET, "/_cat/indices");
		_ = catResponse.Body.Should().Contain("matched\":\"predicate\"");

		var otherResponse = transport.Request<StringResponse>(HttpMethod.GET, "/other");
		_ = otherResponse.Body.Should().Contain("matched\":\"catchall\"");
	}
}
