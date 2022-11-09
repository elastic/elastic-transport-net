// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

namespace Elastic.Transport
{
	/// <summary>
	/// The default implementation for <see cref="IRequestPipeline"/> that returns <see cref="RequestPipeline{TConfiguration}"/>
	/// </summary>
	internal sealed class DefaultRequestPipelineFactory<TConfiguration> : RequestPipelineFactory<TConfiguration>
		where TConfiguration : class, ITransportConfiguration
	{
		/// <summary>
		/// returns instances of <see cref="RequestPipeline{TConfiguration}"/>
		/// </summary>
		public override IRequestPipeline Create(TConfiguration configurationValues, DateTimeProvider dateTimeProvider,
			MemoryStreamFactory memoryStreamFactory, RequestParameters requestParameters) =>
				new RequestPipeline<TConfiguration>(configurationValues, dateTimeProvider, memoryStreamFactory, requestParameters);
	}
}
