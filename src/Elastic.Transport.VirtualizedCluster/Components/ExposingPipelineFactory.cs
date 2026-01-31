// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

namespace Elastic.Transport.VirtualizedCluster.Components;

/// <summary>
/// An implementation that exposes all the components so that <see cref="VirtualCluster"/> can reference them directly.
/// </summary>
public sealed class ExposingPipelineFactory<TConfiguration> : RequestPipelineFactory
	where TConfiguration : class, ITransportConfiguration
{
	public ExposingPipelineFactory(TConfiguration configuration)
	{
		Configuration = configuration;
		Transport = new DistributedTransport<TConfiguration>(Configuration);
	}

	private TConfiguration Configuration { get; }
	public ITransport<TConfiguration> Transport { get; }

	public override RequestPipeline Create(BoundConfiguration boundConfiguration) => new(boundConfiguration);
}
