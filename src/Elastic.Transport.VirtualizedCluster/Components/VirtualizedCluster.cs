// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Threading;
using System.Threading.Tasks;
using Elastic.Transport.VirtualizedCluster.Providers;

namespace Elastic.Transport.VirtualizedCluster.Components;

public class VirtualizedCluster
{
	private readonly ExposingPipelineFactory<ITransportConfiguration> _exposingRequestPipeline;
	private readonly TestableDateTimeProvider _dateTimeProvider;
	private readonly TransportConfigurationDescriptor _settings;

	private Func<ITransport<ITransportConfiguration>, Func<RequestConfigurationDescriptor, IRequestConfiguration>, Task<TransportResponse>> _asyncCall;
	private Func<ITransport<ITransportConfiguration>, Func<RequestConfigurationDescriptor, IRequestConfiguration>, TransportResponse> _syncCall;

	private sealed class VirtualResponse : TransportResponse;

	private static readonly EndpointPath RootPath = new(HttpMethod.GET, "/");

	internal VirtualizedCluster(TransportConfigurationDescriptor settings)
	{
		_settings = settings;
		_dateTimeProvider = ((ITransportConfiguration)_settings).DateTimeProvider as TestableDateTimeProvider
			?? throw new ArgumentException("DateTime provider is not a TestableDateTimeProvider", nameof(settings));
		_exposingRequestPipeline = new ExposingPipelineFactory<ITransportConfiguration>(settings);

		_syncCall = (t, r) => t.Request<VirtualResponse>(
			path: RootPath,
			postData: PostData.Serializable(new { }),
			null,
			localConfiguration: r?.Invoke(new RequestConfigurationDescriptor())
		);
		_asyncCall = async (t, r) =>
		{
			var res = await t.RequestAsync<VirtualResponse>
			(
				path: RootPath,
				postData: PostData.Serializable(new { }),
				null,
				localConfiguration: r?.Invoke(new RequestConfigurationDescriptor()),
				CancellationToken.None
			).ConfigureAwait(false);
			return res;
		};
	}

	public VirtualClusterRequestInvoker Connection => RequestHandler.Configuration.RequestInvoker as VirtualClusterRequestInvoker;
	public NodePool ConnectionPool => RequestHandler.Configuration.NodePool;
	public ITransport<ITransportConfiguration> RequestHandler => _exposingRequestPipeline?.Transport;

	public VirtualizedCluster TransportProxiesTo(
		Func<ITransport<ITransportConfiguration>, Func<RequestConfigurationDescriptor, IRequestConfiguration>, TransportResponse> sync,
		Func<ITransport<ITransportConfiguration>, Func<RequestConfigurationDescriptor, IRequestConfiguration>, Task<TransportResponse>> async
	)
	{
		_syncCall = sync;
		_asyncCall = async;
		return this;
	}

	public TransportResponse ClientCall(Func<RequestConfigurationDescriptor, IRequestConfiguration> requestOverrides = null) =>
		_syncCall(RequestHandler, WithThrows(requestOverrides));

	public async Task<TransportResponse> ClientCallAsync(Func<RequestConfigurationDescriptor, IRequestConfiguration> requestOverrides = null) =>
		await _asyncCall(RequestHandler, WithThrows(requestOverrides)).ConfigureAwait(false);

	public void ChangeTime(Func<DateTimeOffset, DateTimeOffset> change) => _dateTimeProvider.ChangeTime(change);

	// The transport binds its configuration once, when it is created, so changing the settings afterwards has no
	/// effect. Apply the choice to each request instead; an override the caller passes in still wins.
	public void ClientThrows(bool throws) => _throws = throws;

	private bool? _throws;

	private Func<RequestConfigurationDescriptor, IRequestConfiguration> WithThrows(Func<RequestConfigurationDescriptor, IRequestConfiguration> requestOverrides) =>
		_throws is not { } throws
			? requestOverrides
			: d => requestOverrides is null ? d.ThrowExceptions(throws) : requestOverrides(d.ThrowExceptions(throws));
}
