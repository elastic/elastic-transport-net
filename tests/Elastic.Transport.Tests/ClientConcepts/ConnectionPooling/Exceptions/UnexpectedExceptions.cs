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

namespace Elastic.Transport.Tests.ClientConcepts.ConnectionPooling.Exceptions;

public class UnexpectedExceptions
{

	[Fact]
	public async Task UnexpectedExceptionsBubbleOut()
	{
		var audit = new Auditor(() => Virtual.Elasticsearch
			.Bootstrap(10)
			.ClientCalls(r => r.SucceedAlways())
			.ClientCalls(r => r.OnPort(9201).FailAlways(new Exception("boom!")))
			.StaticNodePool()
			.Settings(s => s.DisablePing())
		);

		audit = await audit.TraceCall(
			new ClientCall {
				{ HealthyResponse, 9200 },
			}
		);

		audit = await audit.TraceUnexpectedException(
			new ClientCall {
				{ BadResponse, 9201 },
			},
			(e) =>
			{
				e.FailureReason.Should().Be(PipelineFailure.Unexpected);
				e.InnerException.Should().NotBeNull();
				e.InnerException.Message.Should().Be("boom!");
			}
		);
	}

	[Fact]
	public async Task WillFailOverKnowConnectionExceptionButNotUnexpected()
	{
		var audit = new Auditor(() => Virtual.Elasticsearch.Bootstrap(10)
#if !NETFRAMEWORK
			.ClientCalls(r => r.OnPort(9200).FailAlways(new System.Net.Http.HttpRequestException("recover")))
#else
			.ClientCalls(r => r.OnPort(9200).FailAlways(new System.Net.WebException("recover")))
#endif
			.ClientCalls(r => r.OnPort(9201).FailAlways(new Exception("boom!")))
			.StaticNodePool()
			.Settings(s => s.DisablePing())
		);

		audit = await audit.TraceUnexpectedException(
			new ClientCall {
				{ BadResponse, 9200 },
				{ BadResponse, 9201 },
			},
			(e) =>
			{
				e.FailureReason.Should().Be(PipelineFailure.Unexpected);
				e.InnerException.Should().NotBeNull();
				e.InnerException.Message.Should().Be("boom!");
			}
		);
	}

	[Fact]
	public async Task PingUnexceptedExceptionDoesFailOver()
	{
		var audit = new Auditor(() => Virtual.Elasticsearch.Bootstrap(10)
			.Ping(r => r.OnPort(9200).FailAlways(new Exception("ping exception")))
			.Ping(r => r.OnPort(9201).SucceedAlways())
			.ClientCalls(r => r.OnPort(9201).FailAlways(new Exception("boom!")))
			.StaticNodePool()
			.AllDefaults()
		);

		audit = await audit.TraceUnexpectedException(
			new ClientCall {
				{ PingFailure, 9200 },
				{ PingSuccess, 9201 },
				{ BadResponse, 9201 },
			},
			e =>
			{
				e.FailureReason.Should().Be(PipelineFailure.Unexpected);

				e.InnerException.Should().NotBeNull();
				e.InnerException.Message.Should().Be("boom!");

				e.SeenExceptions.Should().NotBeEmpty();
				var pipelineException = e.SeenExceptions.First();
				pipelineException.FailureReason.Should().Be(PipelineFailure.PingFailure);
				pipelineException.InnerException.Message.Should().Be("ping exception");

				var pingException = e.AuditTrail.First(a => a.Event == PingFailure).Exception;
				pingException.Should().NotBeNull();
				pingException.Message.Should().Be("ping exception");
			}
		);
	}
}
