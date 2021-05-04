// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

namespace Elastic.Transport
{
	/// <summary> A factory that creates instances of <see cref="IRequestPipeline"/>, this factory exists so that transport can be tested. </summary>
	public interface IRequestPipelineFactory<TConfiguration>
		where TConfiguration : class, ITransportConfiguration
	{
		/// <summary> Create an instance of <see cref="IRequestPipeline"/> </summary>
		IRequestPipeline Create(TConfiguration configuration, IDateTimeProvider dateTimeProvider,
			IMemoryStreamFactory memoryStreamFactory, IRequestParameters requestParameters
		);
	}

	/// <summary>
	/// The default implementation for <see cref="IRequestPipeline"/> that returns <see cref="RequestPipeline{TConfiguration}"/>
	/// </summary>
	public class RequestPipelineFactory<TConfiguration> : IRequestPipelineFactory<TConfiguration>
		where TConfiguration : class, ITransportConfiguration
	{
		/// <summary>
		/// returns instances of <see cref="RequestPipeline{TConfiguration}"/>
		/// </summary>
		public IRequestPipeline Create(TConfiguration configurationValues, IDateTimeProvider dateTimeProvider,
			IMemoryStreamFactory memoryStreamFactory, IRequestParameters requestParameters
		) =>
			new RequestPipeline<TConfiguration>(configurationValues, dateTimeProvider, memoryStreamFactory, requestParameters);

	}
}
