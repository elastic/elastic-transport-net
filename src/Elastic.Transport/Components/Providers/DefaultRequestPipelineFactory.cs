// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

namespace Elastic.Transport;

/// <summary>
/// The default implementation for <see cref="RequestPipeline"/> that returns <see cref="DefaultRequestPipeline{TConfiguration}"/>
/// </summary>
internal sealed class DefaultRequestPipelineFactory<TConfiguration> : RequestPipelineFactory<TConfiguration>
	where TConfiguration : class, ITransportConfiguration
{
	/// <summary>
	/// returns instances of <see cref="DefaultRequestPipeline{TConfiguration}"/>
	/// </summary>
	public override RequestPipeline Create(TConfiguration configurationValues, DateTimeProvider dateTimeProvider,
		MemoryStreamFactory memoryStreamFactory, IRequestConfiguration? requestConfiguration) =>
			new DefaultRequestPipeline<TConfiguration>(configurationValues, dateTimeProvider, memoryStreamFactory, requestConfiguration);
}
