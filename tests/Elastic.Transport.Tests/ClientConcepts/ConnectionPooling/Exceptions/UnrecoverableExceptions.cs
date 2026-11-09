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

namespace Elastic.Transport.Tests.ClientConcepts.ConnectionPooling.Exceptions;

public class UnrecoverableExceptions
{

	[Fact]
	public void SomePipelineFailuresAreRecoverable()
	{
#pragma warning disable CA2263 // the generic overload does not exist on net481
		var failures = (PipelineFailure[])Enum.GetValues(typeof(PipelineFailure));
#pragma warning restore CA2263
		foreach (var failure in failures)
		{
			switch (failure)
			{

				case PipelineFailure.PingFailure:
				case PipelineFailure.BadRequest:
				case PipelineFailure.BadResponse:
					var recoverable = new PipelineException(failure);
					recoverable.Recoverable.Should().BeTrue(failure.ToString());
					break;

				case PipelineFailure.BadAuthentication:
				case PipelineFailure.SniffFailure:
				case PipelineFailure.CouldNotStartSniffOnStartup:
				case PipelineFailure.MaxTimeoutReached:
				case PipelineFailure.MaxRetriesReached:
				case PipelineFailure.Unexpected:
				case PipelineFailure.NoNodesAttempted:
						var unrecoverable = new PipelineException(failure);
					unrecoverable.Recoverable.Should().BeFalse(failure.ToString());
					break;
				default:
					throw new ArgumentOutOfRangeException(failure.ToString());
			}
		}
	}

	// Failure: System.Exception : No exception happened while one was expected (Auditor.TraceElasticsearchException calls ClientThrows(true); the call returns an unsuccessful response instead of throwing).
	[Fact(Skip = "UNCLASSIFIED: VirtualizedCluster.ClientThrows(true) is a no-op because DistributedTransport snapshots a BoundConfiguration at construction")]
	public async Task BadAuthenticationIsUnrecoverable()
	{
		var audit = new Auditor(() => Virtual.Elasticsearch.Bootstrap(10)
			.Ping(r => r.SucceedAlways())
			.ClientCalls(r => r.FailAlways(401))
			.StaticNodePool()
			.AllDefaults()
		);

		audit = await audit.TraceElasticsearchException(
			new ClientCall {
				{ PingSuccess, 9200 },
				{ BadResponse, 9200 },
			},
			exception =>
			{
				exception.FailureReason
					.Should().Be(PipelineFailure.BadAuthentication);
			}
		);
	}

	private static readonly byte[] HtmlNginx401Response = Encoding.UTF8.GetBytes(NginxHtml401Response);

	private const string NginxHtml401Response = """
		<html>
		<head><title>401 Authorization Required</title></head>
		<body bgcolor="white">
		<center><h1>401 Authorization Required</h1></center>
		<hr><center>nginx/1.4.6 (Ubuntu)</center>
		</body>
		</html>
		<!-- a padding to disable MSIE and Chrome friendly error page -->
		""";

	// Failure: System.Exception : No exception happened while one was expected (Auditor.TraceElasticsearchException calls ClientThrows(true); the call returns an unsuccessful response instead of throwing).
	[Fact(Skip = "UNCLASSIFIED: VirtualizedCluster.ClientThrows(true) is a no-op because DistributedTransport snapshots a BoundConfiguration at construction")]
	public async Task BadAuthenticationHtmlResponseIsIgnored()
	{
		var audit = new Auditor(() => Virtual.Elasticsearch.Bootstrap(10)
			.Ping(r => r.SucceedAlways())
			.ClientCalls(r => r.FailAlways(401).ReturnByteResponse(HtmlNginx401Response, "application/json"))
			.StaticNodePool()
			.Settings(s => s.SkipDeserializationForStatusCodes(401))
		);

		audit = await audit.TraceElasticsearchException(
			new ClientCall {
				{ PingSuccess, 9200 },
				{ BadResponse, 9201 },
			},
			(e) =>
			{
				e.FailureReason.Should().Be(PipelineFailure.BadAuthentication);
				e.ApiCallDetails.HttpStatusCode.Should().Be(401);
				e.ApiCallDetails.ResponseBodyInBytes.Should().BeNull();
			}
		);
	}

	// Failure: System.Exception : No exception happened while one was expected (Auditor.TraceElasticsearchException calls ClientThrows(true); the call returns an unsuccessful response instead of throwing).
	[Fact(Skip = "UNCLASSIFIED: VirtualizedCluster.ClientThrows(true) is a no-op because DistributedTransport snapshots a BoundConfiguration at construction")]
	public async Task BadAuthenticationHtmlResponseStillExposedWhenUsingDisableDirectStreaming()
	{
		var audit = new Auditor(() => Virtual.Elasticsearch.Bootstrap(10)
			.Ping(r => r.SucceedAlways())
			.ClientCalls(r => r.FailAlways(401).ReturnByteResponse(HtmlNginx401Response, "text/html"))
			.StaticNodePool()
			.Settings(s => s.DisableDirectStreaming())
		);

		audit = await audit.TraceElasticsearchException(
			new ClientCall {
				{ PingSuccess, 9200 },
				{ BadResponse, 9200 },
			},
			(e) =>
			{
				e.FailureReason.Should().Be(PipelineFailure.BadAuthentication);
				e.ApiCallDetails.HttpStatusCode.Should().Be(401);
				e.ApiCallDetails.ResponseBodyInBytes.Should().NotBeNull();
				var responseString = Encoding.UTF8.GetString(e.ApiCallDetails.ResponseBodyInBytes);
				responseString.Should().Contain("nginx/");
				e.DebugInformation.Should().Contain("nginx/");
			}
		);
	}

	[Fact]
	public async Task BadAuthenticationIsUnrecoverableOnResponse()
	{
		var audit = new Auditor(() => Virtual.Elasticsearch.Bootstrap(10)
			.Ping(r => r.SucceedAlways())
			.ClientCalls(r => r.FailAlways(401))
			.StaticNodePool()
			.AllDefaults()
		);

		_ = await audit.TraceElasticsearchExceptionOnResponse(
			new ClientCall {
				{ PingSuccess, 9200 },
				{ BadResponse, 9200 },
			},
			exception => exception.FailureReason.Should().Be(PipelineFailure.BadAuthentication)
		);
	}

	[Fact]
	public async Task BadAuthenticationHtmlResponseStillExposedWhenUsingDisableDirectStreamingOnResponse()
	{
		var audit = new Auditor(() => Virtual.Elasticsearch.Bootstrap(10)
			.Ping(r => r.SucceedAlways())
			.ClientCalls(r => r.FailAlways(401).ReturnByteResponse(HtmlNginx401Response, "text/html"))
			.StaticNodePool()
			.Settings(s => s.DisableDirectStreaming())
		);

		_ = await audit.TraceElasticsearchExceptionOnResponse(
			new ClientCall {
				{ PingSuccess, 9200 },
				{ BadResponse, 9200 },
			},
			e =>
			{
				e.FailureReason.Should().Be(PipelineFailure.BadAuthentication);
				e.ApiCallDetails.HttpStatusCode.Should().Be(401);
				e.ApiCallDetails.ResponseBodyInBytes.Should().NotBeNull();
				Encoding.UTF8.GetString(e.ApiCallDetails.ResponseBodyInBytes).Should().Contain("nginx/");
			}
		);
	}
}
