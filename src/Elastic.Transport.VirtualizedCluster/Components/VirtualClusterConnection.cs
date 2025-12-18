// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Elastic.Transport.VirtualizedCluster.Products;
using Elastic.Transport.VirtualizedCluster.Products.Elasticsearch;
using Elastic.Transport.VirtualizedCluster.Providers;
using Elastic.Transport.VirtualizedCluster.Rules;
#if !NETFRAMEWORK
using TheException = System.Net.Http.HttpRequestException;
#else
using TheException = System.Net.WebException;
#endif

namespace Elastic.Transport.VirtualizedCluster.Components;

/// <summary>
/// An in memory requestInvoker that uses a rule engine to return different responses for sniffs/pings and API calls.
/// <pre>
/// Either instantiate through the static <see cref="Success"/> or <see cref="Error"/> for the simplest use-cases
/// </pre>
/// <pre>
/// Or use <see cref="ElasticsearchVirtualCluster"/> to chain together a rule engine until
/// <see cref="SealedVirtualCluster.VirtualClusterConnection"/> becomes available
/// </pre>
/// </summary>
public class VirtualClusterRequestInvoker : IRequestInvoker
{
	private static readonly object Lock = new();

	private static byte[]? _defaultResponseBytes;

	private VirtualCluster _cluster;
	private readonly TestableDateTimeProvider _dateTimeProvider;
	private MockProductRegistration _productRegistration;
	private IDictionary<int, State> _calls;

	private readonly InMemoryRequestInvoker _inMemoryRequestInvoker;

	internal VirtualClusterRequestInvoker(VirtualCluster cluster, TestableDateTimeProvider dateTimeProvider)
	{
		_cluster = cluster;
		_calls = cluster.Nodes.ToDictionary(n => n.Uri.Port, _ => new State());
		_productRegistration = cluster.ProductRegistration;
		_dateTimeProvider = dateTimeProvider;
		_productRegistration = cluster.ProductRegistration;
		_inMemoryRequestInvoker = new InMemoryRequestInvoker();
	}

	void IDisposable.Dispose() { }

	/// <summary>
	/// Create a <see cref="VirtualClusterRequestInvoker"/> instance that always returns a successful response.
	/// </summary>
	/// <param name="response">The bytes to be returned on every API call invocation</param>
	public static VirtualClusterRequestInvoker Success(byte[] response) =>
		Virtual.Elasticsearch
			.Bootstrap(1)
			.ClientCalls(r => r.SucceedAlways().ReturnByteResponse(response))
			.StaticNodePool()
			.AllDefaults()
			.Connection;

	/// <summary>
	/// Create a <see cref="VirtualClusterRequestInvoker"/> instance that always returns a failed response.
	/// </summary>
	public static VirtualClusterRequestInvoker Error() =>
		Virtual.Elasticsearch
			.Bootstrap(1)
			.ClientCalls(r => r.FailAlways(400))
			.StaticNodePool()
			.AllDefaults()
			.Connection;

	private static object DefaultResponse
	{
		get
		{
			var response = new
			{
				name = "Razor Fist",
				cluster_name = "elasticsearch-test-cluster",
				version = new
				{
					number = "2.0.0",
					build_hash = "af1dc6d8099487755c3143c931665b709de3c764",
					build_timestamp = "2015-07-07T11:28:47Z",
					build_snapshot = true,
					lucene_version = "5.2.1"
				},
				tagline = "You Know, for Search"
			};
			return response;
		}
	}

	public ResponseFactory ResponseFactory => _inMemoryRequestInvoker.ResponseFactory;

	private void UpdateCluster(VirtualCluster cluster)
	{
		lock (Lock)
		{
			_cluster = cluster;
			_calls = cluster.Nodes.ToDictionary(n => n.Uri.Port, _ => new State());
			_productRegistration = cluster.ProductRegistration;
		}
	}

	private bool IsSniffRequest(Endpoint endpoint) => _productRegistration.IsSniffRequest(endpoint);

	private bool IsPingRequest(Endpoint endpoint) => _productRegistration.IsPingRequest(endpoint);

	/// <inheritdoc cref="IRequestInvoker.RequestAsync{TResponse}"/>>
	public Task<TResponse> RequestAsync<TResponse>(Endpoint endpoint, BoundConfiguration boundConfiguration, PostData? postData, CancellationToken cancellationToken)
		where TResponse : TransportResponse, new() =>
		Task.FromResult(Request<TResponse>(endpoint, boundConfiguration, postData));

	/// <inheritdoc cref="IRequestInvoker.Request{TResponse}"/>>
	public TResponse Request<TResponse>(Endpoint endpoint, BoundConfiguration boundConfiguration, PostData? postData)
		where TResponse : TransportResponse, new()
	{
		if (!_calls.ContainsKey(endpoint.Uri.Port))
			throw new Exception($"Expected a call to happen on port {endpoint.Uri.Port} but received none");

		try
		{
			var state = _calls[endpoint.Uri.Port];
			if (IsSniffRequest(endpoint))
			{
				_ = Interlocked.Increment(ref state.Sniffed);
				return HandleRules<TResponse, ISniffRule>(
					endpoint,
					boundConfiguration,
					postData,
					nameof(VirtualCluster.Sniff),
					_cluster.SniffingRules,
					boundConfiguration.RequestTimeout,
					r => UpdateCluster(r.NewClusterState),
					_ => _productRegistration.CreateSniffResponseBytes(_cluster.Nodes, _cluster.ElasticsearchVersion, _cluster.PublishAddressOverride, _cluster.SniffShouldReturnFqnd)
				);
			}
			if (IsPingRequest(endpoint))
			{
				_ = Interlocked.Increment(ref state.Pinged);
				return HandleRules<TResponse, IRule>(
					endpoint,
					boundConfiguration,
					postData,
					nameof(VirtualCluster.Ping),
					_cluster.PingingRules,
					boundConfiguration.PingTimeout,
					_ => { },
					_ => null //HEAD request
				);
			}
			_ = Interlocked.Increment(ref state.Called);
			return HandleRules<TResponse, IClientCallRule>(
				endpoint,
				boundConfiguration,
				postData,
				nameof(VirtualCluster.ClientCalls),
				_cluster.ClientCallRules,
				boundConfiguration.RequestTimeout,
				_ => { },
				CallResponse
			);
		}
		catch (TheException e)
		{
			return ResponseFactory.Create<TResponse>(endpoint, boundConfiguration, postData, e, null, null, Stream.Null, null, -1, null, null);
		}
	}

	private TResponse HandleRules<TResponse, TRule>(
		Endpoint endpoint,
		BoundConfiguration boundConfiguration,
		PostData? postData,
		string origin,
		IList<TRule> rules,
		TimeSpan timeout,
		Action<TRule> beforeReturn,
		Func<TRule, byte[]?> successResponse
	)
		where TResponse : TransportResponse, new()
		where TRule : IRule
	{
		if (rules.Count == 0)
			throw new Exception($"No {origin} defined for the current VirtualCluster, so we do not know how to respond");

		foreach (var rule in rules.Where(s => s.OnPort.HasValue))
		{
			var always = rule.Times.Match(_ => true, _ => false);
			var times = rule.Times.Match(_ => -1, t => t);

			if (rule.OnPort == null || rule.OnPort.Value != endpoint.Uri.Port)
				continue;

			if (always)
				return Always<TResponse, TRule>(endpoint, boundConfiguration, postData, timeout, beforeReturn, successResponse, rule);

			if (rule.ExecuteCount > times)
				continue;

			return Sometimes<TResponse, TRule>(endpoint, boundConfiguration, postData, timeout, beforeReturn, successResponse, rule);
		}
		foreach (var rule in rules.Where(s => !s.OnPort.HasValue))
		{
			var always = rule.Times.Match(_ => true, _ => false);
			var times = rule.Times.Match(_ => -1, t => t);
			if (always)
				return Always<TResponse, TRule>(endpoint, boundConfiguration, postData, timeout, beforeReturn, successResponse, rule);

			if (rule.ExecuteCount > times)
				continue;

			return Sometimes<TResponse, TRule>(endpoint, boundConfiguration, postData, timeout, beforeReturn, successResponse, rule);
		}
		var count = _calls.Sum(kv => kv.Value.Called);
		throw new Exception($@"No global or port specific {origin} rule ({endpoint.Uri.Port}) matches any longer after {count} calls in to the cluster");
	}

	private TResponse Always<TResponse, TRule>(Endpoint endpoint, BoundConfiguration boundConfiguration, PostData? postData, TimeSpan timeout, Action<TRule> beforeReturn, Func<TRule, byte[]?> successResponse, TRule rule
	)
		where TResponse : TransportResponse, new()
		where TRule : IRule
	{
		if (rule.Takes.HasValue)
		{
			var time = timeout < rule.Takes.Value ? timeout : rule.Takes.Value;
			_dateTimeProvider.ChangeTime(d => d.Add(time));
			if (rule.Takes.Value > boundConfiguration.RequestTimeout)
				throw new TheException(
					$"Request timed out after {time} : call configured to take {rule.Takes.Value} while requestTimeout was: {timeout}");
		}

		return rule.Succeeds
			? Success<TResponse, TRule>(endpoint, boundConfiguration, postData, beforeReturn, successResponse, rule)
			: Fail<TResponse, TRule>(endpoint, boundConfiguration, postData, rule);
	}

	private TResponse Sometimes<TResponse, TRule>(
		Endpoint endpoint, BoundConfiguration boundConfiguration, PostData? postData, TimeSpan timeout, Action<TRule> beforeReturn, Func<TRule, byte[]?> successResponse, TRule rule
	)
		where TResponse : TransportResponse, new()
		where TRule : IRule
	{
		if (rule.Takes.HasValue)
		{
			var time = timeout < rule.Takes.Value ? timeout : rule.Takes.Value;
			_dateTimeProvider.ChangeTime(d => d.Add(time));
			if (rule.Takes.Value > boundConfiguration.RequestTimeout)
				throw new TheException(
					$"Request timed out after {time} : call configured to take {rule.Takes.Value} while requestTimeout was: {timeout}");
		}

		if (rule.Succeeds)
			return Success<TResponse, TRule>(endpoint, boundConfiguration, postData, beforeReturn, successResponse, rule);

		return Fail<TResponse, TRule>(endpoint, boundConfiguration, postData, rule);
	}

	private TResponse Fail<TResponse, TRule>(Endpoint endpoint, BoundConfiguration boundConfiguration, PostData? postData, TRule rule, RuleOption<Exception, int>? returnOverride = null)
		where TResponse : TransportResponse, new()
		where TRule : IRule
	{
		var state = _calls[endpoint.Uri.Port];
		_ = Interlocked.Increment(ref state.Failures);
		var ret = returnOverride ?? rule.Return;
		rule.RecordExecuted();

		if (ret == null)
			throw new TheException();

		return ret.Match(
			e => throw e,
			statusCode => _inMemoryRequestInvoker.BuildResponse<TResponse>(endpoint, boundConfiguration, postData, CallResponse(rule),
				//make sure we never return a valid status code in Fail responses because of a bad rule.
				statusCode is >= 200 and < 300 ? 502 : statusCode, rule.ReturnContentType)
		);
	}

	private TResponse Success<TResponse, TRule>(Endpoint endpoint, BoundConfiguration boundConfiguration, PostData? postData, Action<TRule> beforeReturn, Func<TRule, byte[]?> successResponse,
		TRule rule
	)
		where TResponse : TransportResponse, new()
		where TRule : IRule
	{
		var state = _calls[endpoint.Uri.Port];
		_ = Interlocked.Increment(ref state.Successes);
		rule.RecordExecuted();

		beforeReturn.Invoke(rule);
		return _inMemoryRequestInvoker.BuildResponse<TResponse>(endpoint, boundConfiguration, postData, successResponse(rule), contentType: rule.ReturnContentType);
	}

	private static byte[] CallResponse<TRule>(TRule rule)
		where TRule : IRule
	{
		if (rule.ReturnResponse != null)
			return rule.ReturnResponse;

		if (_defaultResponseBytes != null)
			return _defaultResponseBytes;

		var response = DefaultResponse;
		using (var ms = TransportConfiguration.DefaultMemoryStreamFactory.Create())
		{
			LowLevelRequestResponseSerializer.Instance.Serialize(response, ms);
			_defaultResponseBytes = ms.ToArray();
		}
		return _defaultResponseBytes;
	}

	private class State
	{
		public int Called;
		public int Failures;
		public int Pinged;
		public int Sniffed;
		public int Successes;
	}
}
