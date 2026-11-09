// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace Elastic.Transport.Tests.ClientConcepts.ConnectionPooling.BuildingBlocks;

/// <summary> An <see cref="IRequestInvoker"/> that waits before returning a fixed response </summary>
public sealed class WaitingInMemoryRequestInvoker(TimeSpan waitTime, byte[] responseBody) : IRequestInvoker
{
	private readonly InMemoryRequestInvoker _inner = new(responseBody);

	public ResponseFactory ResponseFactory => _inner.ResponseFactory;

	public TResponse Request<TResponse>(Endpoint endpoint, BoundConfiguration boundConfiguration, PostData postData)
		where TResponse : TransportResponse, new()
	{
		Thread.Sleep(waitTime);
		return _inner.Request<TResponse>(endpoint, boundConfiguration, postData);
	}

	public async Task<TResponse> RequestAsync<TResponse>(Endpoint endpoint, BoundConfiguration boundConfiguration, PostData postData, CancellationToken cancellationToken)
		where TResponse : TransportResponse, new()
	{
		await Task.Delay(waitTime, cancellationToken).ConfigureAwait(false);
		return await _inner.RequestAsync<TResponse>(endpoint, boundConfiguration, postData, cancellationToken).ConfigureAwait(false);
	}

	public void Dispose() => ((IDisposable)_inner).Dispose();
}

public class RequestPipelines
{
	private static readonly byte[] SniffResponse = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
	{
		cluster_name = "elasticsearch",
		nodes = new
		{
			node1 = new
			{
				name = "Node Name 1",
				transport_address = "127.0.0.1:9300",
				host = "127.0.0.1",
				ip = "127.0.01",
				version = "5.0.0-alpha3",
				build_hash = "e455fd0",
				roles = new List<string>(),
				http = new { bound_address = new[] { "127.0.0.1:9200" } },
				settings = new Dictionary<string, object> { { "cluster.name", "elasticsearch" }, { "node.name", "Node Name 1" } }
			}
		}
	}));

	[Fact]
	public void RequestPipelineIsCreatedByTheFactory()
	{
		var config = new TransportConfiguration(new SingleNodePool(new Uri("http://localhost:9200")), new InMemoryRequestInvoker());
		var bound = new BoundConfiguration(config);

		var pipeline = DefaultRequestPipelineFactory.Default.Create(bound);

		pipeline.Should().BeOfType<RequestPipeline>();
	}

	[Fact]
	public void FirstUsageCheckConcurrentThreads()
	{
		var uris = new[] { new Uri("http://localhost:9200"), new Uri("http://localhost:9201") };
		var invoker = new WaitingInMemoryRequestInvoker(TimeSpan.FromSeconds(1), SniffResponse);
		var config = new TransportConfigurationDescriptor(new SniffingNodePool(uris), invoker)
			.RequestTimeout(TimeSpan.FromSeconds(2));
		var pipeline = DefaultRequestPipelineFactory.Default.Create(new BoundConfiguration(config));

		var semaphore = new SemaphoreSlim(1, 1);
		var task1 = Task.Run(() => pipeline.FirstPoolUsage(semaphore, null));
		var task2 = Task.Run(() => pipeline.FirstPoolUsage(semaphore, null));
		var task3 = Task.Run(() => pipeline.FirstPoolUsage(semaphore, null));

		var exception = Record.Exception(() => Task.WaitAll(task1, task2, task3));

		exception.Should().BeNull();
		semaphore.CurrentCount.Should().Be(1);
	}
}
