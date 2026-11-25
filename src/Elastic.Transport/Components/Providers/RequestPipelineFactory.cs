// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

namespace Elastic.Transport;

/// <summary> A factory that creates instances of <c>RequestPipeline</c>, this factory exists so that transport can be tested. </summary>
internal abstract class RequestPipelineFactory
{
	internal RequestPipelineFactory() { }

	/// <summary> Create an instance of <c>RequestPipeline</c> </summary>
	public abstract RequestPipeline Create(BoundConfiguration boundConfiguration);
}

/// <summary>Implemented by configurations that can say how to create pipelines. Internal: the pipeline is not an extension point.</summary>
internal interface IHasPipelineProvider
{
	RequestPipelineFactory? PipelineProvider { get; }
}
