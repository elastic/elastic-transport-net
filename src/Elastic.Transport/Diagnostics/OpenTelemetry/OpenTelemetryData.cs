// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Collections.Generic;

namespace Elastic.Transport.Diagnostics;

/// <summary>
/// Allows consumers to pass specific values for OpenTelemetry instrumentation for each request.
/// </summary>
public readonly struct OpenTelemetryData
{
	/// <summary>
	/// The name to use for spans relating to a request.
	/// </summary>
	public string? SpanName { get; init; }

	/// <summary>
	/// Additional span attributes for transport spans relating to a request.
	/// </summary>
	public Dictionary<string, object>? SpanAttributes { get; init; }
}
