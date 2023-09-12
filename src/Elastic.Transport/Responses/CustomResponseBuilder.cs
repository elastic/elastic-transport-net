// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Elastic.Transport;

/// <summary>
/// Allows callers to override completely how `TResponse` should be deserialized to a `TResponse` that implements <see cref="TransportResponse"/> instance.
/// <para>Expert setting only</para>
/// </summary>
public abstract class CustomResponseBuilder
{
	/// <summary> Custom routine that deserializes from <paramref name="stream"/> to an instance of <see cref="TransportResponse"/>.</summary>
	public abstract object DeserializeResponse(Serializer serializer, ApiCallDetails response, Stream stream);

	/// <inheritdoc cref="DeserializeResponse"/>
	public abstract Task<object> DeserializeResponseAsync(Serializer serializer, ApiCallDetails response, Stream stream, CancellationToken ctx = default);
}
