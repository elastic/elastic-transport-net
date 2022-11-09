// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Diagnostics;
using Elastic.Transport.Diagnostics;

//#if NETSTANDARD2_0 || NETSTANDARD2_1
//using System.Threading.Tasks.Extensions;
//#endif

namespace Elastic.Transport;

internal static class RequestPipelineStatics
{
	public static readonly string NoNodesAttemptedMessage =
		"No nodes were attempted, this can happen when a node predicate does not match any nodes";

	public static DiagnosticSource DiagnosticSource { get; } = new DiagnosticListener(DiagnosticSources.RequestPipeline.SourceName);
}
#pragma warning restore 1591
