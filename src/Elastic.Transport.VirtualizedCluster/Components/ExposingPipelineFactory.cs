// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

namespace Elastic.Transport.VirtualizedCluster.Components;

/// <summary>
/// An implementation that exposes all the components so that <see cref="VirtualCluster"/> can reference them directly.
/// </summary>
public sealed class ExposingPipelineFactory<TConfiguration> : RequestPipelineFactory<TConfiguration> where TConfiguration : class, ITransportConfiguration
{
	public ExposingPipelineFactory(TConfiguration configuration, DateTimeProvider dateTimeProvider)
	{
		DateTimeProvider = dateTimeProvider;
		MemoryStreamFactory = TransportConfiguration.DefaultMemoryStreamFactory;
		Configuration = configuration;
		Pipeline = Create(Configuration, DateTimeProvider, MemoryStreamFactory, new DefaultRequestParameters());
		RequestHandler = new DistributedTransport<TConfiguration>(Configuration, this, DateTimeProvider, MemoryStreamFactory);
	}

	// ReSharper disable once MemberCanBePrivate.Global
	public RequestPipeline Pipeline { get; }
	private DateTimeProvider DateTimeProvider { get; }
	private MemoryStreamFactory MemoryStreamFactory { get; }
	private TConfiguration Configuration { get; }
	public ITransport<TConfiguration> RequestHandler { get; }

	public override RequestPipeline Create(TConfiguration configurationValues, DateTimeProvider dateTimeProvider,
		MemoryStreamFactory memoryStreamFactory, RequestParameters requestParameters) =>
			new DefaultRequestPipeline<TConfiguration>(Configuration, DateTimeProvider, MemoryStreamFactory, requestParameters ?? new DefaultRequestParameters());
}
