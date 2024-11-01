// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

namespace Elastic.Transport;

/// <summary>
/// The default implementation for <see cref="RequestPipeline"/> that returns <see cref="DefaultRequestPipeline"/>
/// </summary>
internal sealed class DefaultRequestPipelineFactory : RequestPipelineFactory
{
	/// <summary>
	/// returns instances of <see cref="DefaultRequestPipeline"/>
	/// </summary>
	public override RequestPipeline Create(RequestData requestData, DateTimeProvider dateTimeProvider) =>
			new DefaultRequestPipeline(requestData, dateTimeProvider);
}
