// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

#nullable enable
namespace Elastic.Transport.VirtualizedCluster.Components;

/// <summary>
/// An implementation that exposes all the components so that <see cref="VirtualCluster"/> can reference them directly.
/// </summary>
public sealed class ExposingPipelineFactory<TConfiguration> : RequestPipelineFactory
	where TConfiguration : class, ITransportConfiguration
{
	public ExposingPipelineFactory(TConfiguration configuration, DateTimeProvider dateTimeProvider)
	{
		DateTimeProvider = dateTimeProvider;
		Configuration = configuration;
		Pipeline = Create(new RequestData(Configuration, null, null), DateTimeProvider);
		RequestHandler = new DistributedTransport<TConfiguration>(Configuration, this, DateTimeProvider);
	}

	// ReSharper disable once MemberCanBePrivate.Global
	public RequestPipeline Pipeline { get; }
	private DateTimeProvider DateTimeProvider { get; }
	private TConfiguration Configuration { get; }
	public ITransport<TConfiguration> RequestHandler { get; }

	public override RequestPipeline Create(RequestData requestData, DateTimeProvider dateTimeProvider) =>
			new DefaultRequestPipeline(requestData, DateTimeProvider);
}
