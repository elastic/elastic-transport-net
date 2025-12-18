// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;

namespace Elastic.Transport.Diagnostics;

/// <summary> Provides a typed listener to  actions that <see cref="RequestPipeline"/> takes e.g sniff, ping, or making an API call </summary>;
internal sealed class RequestPipelineDiagnosticObserver : TypedDiagnosticObserver<BoundConfiguration, ApiCallDetails>
{
	/// <inheritdoc cref="RequestPipelineDiagnosticObserver"/>
	public RequestPipelineDiagnosticObserver(
		Action<KeyValuePair<string, BoundConfiguration>> onNextStart,
		Action<KeyValuePair<string, ApiCallDetails>> onNextEnd,
		Action<Exception>? onError = null,
		Action? onCompleted = null
	) : base(onNextStart, onNextEnd, onError, onCompleted) { }

}
