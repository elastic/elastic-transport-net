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

	private Func<HttpTransport<ITransportConfiguration>, Func<RequestConfigurationDescriptor, IRequestConfiguration>, Task<TransportResponse>> _asyncCall;
	private Func<HttpTransport<ITransportConfiguration>, Func<RequestConfigurationDescriptor, IRequestConfiguration>, TransportResponse> _syncCall;

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

	public VirtualClusterTransportClient Connection => Transport.Settings.Connection as VirtualClusterTransportClient;
	public NodePool ConnectionPool => Transport.Settings.NodePool;
	public HttpTransport<ITransportConfiguration> Transport => _exposingRequestPipeline?.Transport;

	public VirtualizedCluster TransportProxiesTo(
		Func<HttpTransport<ITransportConfiguration>, Func<RequestConfigurationDescriptor, IRequestConfiguration>, TransportResponse> sync,
		Func<HttpTransport<ITransportConfiguration>, Func<RequestConfigurationDescriptor, IRequestConfiguration>, Task<TransportResponse>> async
	)
	{
		_syncCall = sync;
		_asyncCall = async;
		return this;
	}

	public TransportResponse ClientCall(Func<RequestConfigurationDescriptor, IRequestConfiguration> requestOverrides = null) =>
		_syncCall(Transport, requestOverrides);

	public async Task<TransportResponse> ClientCallAsync(Func<RequestConfigurationDescriptor, IRequestConfiguration> requestOverrides = null) =>
		await _asyncCall(Transport, requestOverrides).ConfigureAwait(false);

	public void ChangeTime(Func<DateTimeOffset, DateTimeOffset> change) => _dateTimeProvider.ChangeTime(change);

	public void ClientThrows(bool throws) => _settings.ThrowExceptions(throws);
}
