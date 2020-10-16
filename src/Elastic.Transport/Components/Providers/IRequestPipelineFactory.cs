// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.Transport.Products;

namespace Elastic.Transport
{
	/// <summary> A factory that creates instances of <see cref="IRequestPipeline"/>, this factory exists so that transport can be tested. </summary>
	public interface IRequestPipelineFactory
	{
		/// <summary> Create an instance of <see cref="IRequestPipeline"/> </summary>
		IRequestPipeline Create(ITransportConfigurationValues configurationValues, IDateTimeProvider dateTimeProvider,
			IMemoryStreamFactory memoryStreamFactory, IRequestParameters requestParameters
		);
	}

	/// <summary>
	/// The default implementation for <see cref="IRequestPipeline"/> that returns <see cref="RequestPipeline"/>
	/// </summary>
	public class RequestPipelineFactory : IRequestPipelineFactory
	{
		/// <summary>
		/// returns instances of <see cref="RequestPipeline"/>
		/// </summary>
		public IRequestPipeline Create(ITransportConfigurationValues configurationValues, IDateTimeProvider dateTimeProvider,
			IMemoryStreamFactory memoryStreamFactory, IRequestParameters requestParameters
		) =>
			new RequestPipeline(configurationValues, dateTimeProvider, memoryStreamFactory, requestParameters);
	}
}
