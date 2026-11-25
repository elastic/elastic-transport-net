// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Linq;
using System.Net.Http;
using BenchmarkDotNet.Attributes;

namespace Elastic.Transport.Benchmarks;

/// <summary>
/// Per-request cost of the pipeline itself (an in-memory invoker, so no sockets) and of the HttpClient factory in
/// isolation. Allocated bytes are exact; timings are indicative only.
/// </summary>
public class PipelineBenchmarks : IDisposable
{
	private ITransport _single = null!;
	private ITransport _staticPool = null!;
	private ITransport _staticPoolWithHeaders = null!;
	private RequestConfiguration _cachedLocal = null!;
	private BoundConfiguration _preBound = null!;
	private static readonly RequestConfiguration NoAudit = new() { DisableAuditTrail = true };
	private BoundConfigurationHttpClientFactory _factory = null!;
	private BoundConfiguration _bound = null!;
	private static readonly int[] Ports = [9200, 9201, 9202];
	private static readonly EndpointPath Root = new(Elastic.Transport.HttpMethod.GET, "/");

	[GlobalSetup]
	public void Setup()
	{
		var invoker = new InMemoryRequestInvoker();

		_single = new DistributedTransport(new TransportConfiguration(new SingleNodePool(new Uri("http://localhost:9200")), invoker));

		var nodes = Ports.Select(p => new Node(new Uri($"http://localhost:{p}")));
		_staticPool = new DistributedTransport(new TransportConfiguration(new StaticNodePool(nodes, randomize: false), invoker));

		var headers = new System.Collections.Specialized.NameValueCollection { { "X-Team", "search" }, { "X-Env", "prod" }, { "X-Trace", "abc" } };
		_staticPoolWithHeaders = new DistributedTransport(new TransportConfiguration(new StaticNodePool(nodes, randomize: false), invoker) { Headers = headers });

		_cachedLocal = new RequestConfiguration { MaxRetries = 2 };
		_preBound = BoundConfiguration.Create((ITransport<ITransportConfiguration>)_staticPool, new RequestConfiguration { MaxRetries = 2 });

		var settings = new TransportConfiguration(new Uri("http://localhost:9200"));
		_bound = new BoundConfiguration(settings);
		_factory = new BoundConfigurationHttpClientFactory(_ => new SocketsHttpHandler());
	}

	[GlobalCleanup]
	public void Cleanup() => Dispose();

	public void Dispose()
	{
		_factory.Dispose();
		GC.SuppressFinalize(this);
	}

	[Benchmark(Baseline = true)]
	public TransportResponse SingleNode() => _single.Request<VoidResponse>(Root, null, null, null);

	/// <summary>The audit trail is on by default; this is the same request with it off, to see what it costs.</summary>
	[Benchmark]
	public TransportResponse SingleNodeWithoutAuditTrail() => _single.Request<VoidResponse>(Root, null, null, NoAudit);

	[Benchmark]
	public TransportResponse StaticPoolOfThree() => _staticPool.Request<VoidResponse>(Root, null, null, null);

	/// <summary>Per-request configuration that is reused: the bound configuration is cached.</summary>
	[Benchmark]
	public TransportResponse StaticPoolReusedRequestConfig() => _staticPool.Request<VoidResponse>(Root, null, null, _cachedLocal);

	/// <summary>The purpose of BoundConfiguration.Create: resolve the request configuration once, pass it on every call.</summary>
	[Benchmark]
	public TransportResponse StaticPoolPreBoundRequestConfig() => _staticPool.Request<VoidResponse>(Root, null, null, _preBound);

	/// <summary>Per-request configuration built on every call: it is bound (merged with the transport's) every time.</summary>
	[Benchmark]
	public TransportResponse StaticPoolFreshRequestConfig() =>
		_staticPool.Request<VoidResponse>(Root, null, null, new RequestConfiguration { MaxRetries = 2 });

	/// <summary>A request config built per call on a transport that has global headers: the headers are merged on every bind.</summary>
	[Benchmark]
	public TransportResponse FreshRequestConfigWithGlobalHeaders() =>
		_staticPoolWithHeaders.Request<VoidResponse>(Root, null, null, new RequestConfiguration { MaxRetries = 2 });

	/// <summary>What the HTTP invoker does per request: ask the factory for a client over a cached handler.</summary>
	[Benchmark]
	public HttpClient FactoryCreateClient() => _factory.CreateClient(_bound);
}
