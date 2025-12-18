// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Elastic.Transport.Diagnostics;

namespace Elastic.Transport;

/// <summary>
/// Attempts to build a response for any type by deserializing the response stream.
/// </summary>
internal sealed class DefaultResponseBuilder : IResponseBuilder
{
	/// <inheritdoc/>
	bool IResponseBuilder.CanBuild<TResponse>() => true;

	/// <inheritdoc/>
	public TResponse? Build<TResponse>(ApiCallDetails apiCallDetails, BoundConfiguration boundConfiguration,
		Stream responseStream, string contentType, long contentLength)
		where TResponse : TransportResponse, new() =>
			SetBodyCoreAsync<TResponse>(false, apiCallDetails, boundConfiguration, responseStream).EnsureCompleted();

	/// <inheritdoc/>
	public Task<TResponse?> BuildAsync<TResponse>(
		ApiCallDetails apiCallDetails, BoundConfiguration boundConfiguration, Stream responseStream, string contentType, long contentLength,
		CancellationToken cancellationToken) where TResponse : TransportResponse, new() =>
			SetBodyCoreAsync<TResponse>(true, apiCallDetails, boundConfiguration, responseStream, cancellationToken).AsTask();

	private static async ValueTask<TResponse?> SetBodyCoreAsync<TResponse>(bool isAsync,
		ApiCallDetails details, BoundConfiguration boundConfiguration, Stream responseStream,
		CancellationToken cancellationToken = default)
		where TResponse : TransportResponse, new()
	{
		TResponse? response = null;

		if (details.HttpStatusCode.HasValue &&
			boundConfiguration.SkipDeserializationForStatusCodes.Contains(details.HttpStatusCode.Value))
			return response;

		try
		{
			var beforeTicks = Stopwatch.GetTimestamp();

			if (isAsync)
				response = await boundConfiguration.ConnectionSettings.RequestResponseSerializer.DeserializeAsync<TResponse>(responseStream, cancellationToken).ConfigureAwait(false);
			else
				response = boundConfiguration.ConnectionSettings.RequestResponseSerializer.Deserialize<TResponse>(responseStream);

			var deserializeResponseMs = (Stopwatch.GetTimestamp() - beforeTicks) / (Stopwatch.Frequency / 1000);

			if (deserializeResponseMs > OpenTelemetry.MinimumMillisecondsToEmitTimingSpanAttribute && OpenTelemetry.CurrentSpanIsElasticTransportOwnedHasListenersAndAllDataRequested)
				Activity.Current?.SetTag(OpenTelemetryAttributes.ElasticTransportDeserializeResponseMs, deserializeResponseMs);

			return response;
		}
		catch (JsonException ex) when (ex.Message.Contains("The input does not contain any JSON tokens"))
		{
			return response;
		}
	}
}
