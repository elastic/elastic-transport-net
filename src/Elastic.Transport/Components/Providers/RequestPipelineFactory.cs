// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

namespace Elastic.Transport
{
	/// <summary> A factory that creates instances of <see cref="IRequestPipeline"/>, this factory exists so that transport can be tested. </summary>
	public abstract class RequestPipelineFactory<TConfiguration>
		where TConfiguration : class, ITransportConfiguration
	{
		internal RequestPipelineFactory() { }

		/// <summary> Create an instance of <see cref="IRequestPipeline"/> </summary>
		public abstract IRequestPipeline Create(TConfiguration configuration, DateTimeProvider dateTimeProvider,
			MemoryStreamFactory memoryStreamFactory, IRequestParameters requestParameters);
	}
}
