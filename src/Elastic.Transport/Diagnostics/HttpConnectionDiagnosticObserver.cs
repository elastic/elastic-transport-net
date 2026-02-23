// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;

namespace Elastic.Transport.Diagnostics;

/// <summary> Provides a typed listener to the events that <see cref="HttpRequestInvoker"/> emits </summary>
/// <inheritdoc cref="HttpConnectionDiagnosticObserver"/>>
internal sealed class HttpConnectionDiagnosticObserver(
	Action<KeyValuePair<string, BoundConfiguration>> onNextStart,
	Action<KeyValuePair<string, int?>> onNextEnd,
	Action<Exception>? onError = null,
	Action? onCompleted = null
	) : TypedDiagnosticObserver<BoundConfiguration, int?>(onNextStart, onNextEnd, onError, onCompleted)
{
}
