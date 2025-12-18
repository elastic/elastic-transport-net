// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;

namespace Elastic.Transport.Diagnostics;

/// <summary> Provides a typed listener any time an <see cref="Serializer"/> does a write or read</summary>
/// <inheritdoc cref="SerializerDiagnosticObserver"/>
internal sealed class SerializerDiagnosticObserver(
	Action<KeyValuePair<string, SerializerRegistrationInformation>> onNext,
	Action<Exception>? onError = null,
	Action? onCompleted = null
	) : TypedDiagnosticObserver<SerializerRegistrationInformation>(onNext, onError, onCompleted)
{
}
