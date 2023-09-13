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
	private readonly TransportConfiguration _settings;

	private Func<ITransport<ITransportConfiguration>, Func<RequestConfigurationDescriptor, IRequestConfiguration>, Task<TransportResponse>> _asyncCall;
	private Func<ITransport<ITransportConfiguration>, Func<RequestConfigurationDescriptor, IRequestConfiguration>, TransportResponse> _syncCall;

	private class VirtualResponse : TransportResponse { }

	internal VirtualizedCluster(TestableDateTimeProvider dateTimeProvider, TransportConfiguration settings)
	{
		_dateTimeProvider = dateTimeProvider;
		_settings = settings;
		_exposingRequestPipeline = new ExposingPipelineFactory<ITransportConfiguration>(settings, _dateTimeProvider);

		_syncCall = (t, r) => t.Request<VirtualResponse>(
			HttpMethod.GET, "/",
			PostData.Serializable(new {}), new DefaultRequestParameters()
		{
				RequestConfiguration = r?.Invoke(new RequestConfigurationDescriptor(null))
		});
		_asyncCall = async (t, r) =>
		{
			var res = await t.RequestAsync<VirtualResponse>
			(
				HttpMethod.GET, "/",
				PostData.Serializable(new { }),
				new DefaultRequestParameters()
				{
					RequestConfiguration = r?.Invoke(new RequestConfigurationDescriptor(null))
				},
				CancellationToken.None
			).ConfigureAwait(false);
			return (TransportResponse)res;
		};
	}

	public VirtualClusterRequestInvoker Connection => RequestHandler.Configuration.Connection as VirtualClusterRequestInvoker;
	public NodePool ConnectionPool => RequestHandler.Configuration.NodePool;
	public ITransport<ITransportConfiguration> RequestHandler => _exposingRequestPipeline?.RequestHandler;

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
		_syncCall(RequestHandler, requestOverrides);

	public async Task<TransportResponse> ClientCallAsync(Func<RequestConfigurationDescriptor, IRequestConfiguration> requestOverrides = null) =>
		await _asyncCall(RequestHandler, requestOverrides).ConfigureAwait(false);

	public void ChangeTime(Func<DateTimeOffset, DateTimeOffset> change) => _dateTimeProvider.ChangeTime(change);

	public void ClientThrows(bool throws) => _settings.ThrowExceptions(throws);
}
