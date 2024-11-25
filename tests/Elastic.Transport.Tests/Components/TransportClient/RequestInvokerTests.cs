// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Xunit;
using System;
using FluentAssertions;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using Elastic.Transport.Diagnostics;
using System.Net.NetworkInformation;

namespace Elastic.Transport.Tests.Components.TransportClient
{
	public class RequestInvokerTests
	{
		[Fact]
		public void NoExceptionShouldBeThrown_WhenHttpResponseDoesNotIncludeCloudHeaders()
		{
			// This test validates that if `ProductRegistration.ParseOpenTelemetryAttributesFromApiCallDetails` returns null,
			// no exception is thrown and attributes are not set.

			using var listener = new ActivityListener
			{
				ActivityStarted = _ => { },
				ActivityStopped = activity => { },
				ShouldListenTo = activitySource => activitySource.Name == OpenTelemetry.ElasticTransportActivitySourceName,
				Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
			};
			ActivitySource.AddActivityListener(listener);

			var requestInvoker = new HttpRequestInvoker(new TestResponseFactory());
			var pool = new SingleNodePool(new Uri("http://localhost:9200"));
			var config = new TransportConfiguration(pool, requestInvoker);
			var transport = new DistributedTransport(config);

			var response = transport.Head("/");
			response.ApiCallDetails.HttpStatusCode.Should().Be(200);
		}

		private sealed class TestResponseFactory : ResponseFactory
		{
			public override TResponse Create<TResponse>(
				Endpoint endpoint,
				BoundConfiguration boundConfiguration,
				PostData postData,
				Exception ex,
				int? statusCode,
				Dictionary<string, IEnumerable<string>> headers,
				Stream responseStream,
				string contentType,
				long contentLength,
				IReadOnlyDictionary<string, ThreadPoolStatistics> threadPoolStats,
				IReadOnlyDictionary<TcpState, int> tcpStats) => CreateResponse<TResponse>();

			public override Task<TResponse> CreateAsync<TResponse>(
				Endpoint endpoint,
				BoundConfiguration boundConfiguration,
				PostData postData,
				Exception ex,
				int? statusCode,
				Dictionary<string, IEnumerable<string>> headers,
				Stream responseStream,
				string contentType,
				long contentLength,
				IReadOnlyDictionary<string, ThreadPoolStatistics> threadPoolStats,
				IReadOnlyDictionary<TcpState, int> tcpStats,
				CancellationToken cancellationToken = default)
			{
				var response = CreateResponse<TResponse>();
				return Task.FromResult(response);
			}

			private static TResponse CreateResponse<TResponse>() where TResponse : TransportResponse, new() => new TResponse
			{
				ApiCallDetails = new() { HttpStatusCode = 200, Uri = new Uri("http://localhost/") }
			};
		}
	}
}
