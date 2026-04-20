// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Elastic.Transport.Diagnostics;

namespace Elastic.Transport.Products.Elasticsearch;

/// <summary>
/// Catch-all builder for <see cref="ElasticsearchResponse"/> subclasses.
/// Handles JSON deserialization for strongly-typed response types.
/// <para>
/// Error extraction is handled by the <see cref="DefaultResponseFactory"/> via
/// <see cref="ProductRegistration.TryExtractError"/> before this builder is invoked.
/// </para>
/// </summary>
internal sealed class ElasticsearchResponseBuilder : IResponseBuilder
{
	bool IResponseBuilder.CanBuild<TResponse>() => true;

	public TResponse? Build<TResponse>(ApiCallDetails apiCallDetails, BoundConfiguration boundConfiguration,
		Stream responseStream, string contentType, long contentLength)
		where TResponse : TransportResponse, new() =>
			SetBodyCoreAsync<TResponse>(false, apiCallDetails, boundConfiguration, responseStream, cancellationToken: default).EnsureCompleted();

	public Task<TResponse?> BuildAsync<TResponse>(
		ApiCallDetails apiCallDetails, BoundConfiguration boundConfiguration, Stream responseStream, string contentType, long contentLength,
		CancellationToken cancellationToken) where TResponse : TransportResponse, new() =>
			SetBodyCoreAsync<TResponse>(true, apiCallDetails, boundConfiguration, responseStream, cancellationToken).AsTask();

	private static async ValueTask<TResponse?> SetBodyCoreAsync<TResponse>(bool isAsync,
		ApiCallDetails details, BoundConfiguration boundConfiguration, Stream responseStream,
		CancellationToken cancellationToken)
		where TResponse : TransportResponse, new()
	{
		TResponse? response = null;

		if (details.HttpStatusCode.HasValue &&
			boundConfiguration.SkipDeserializationForStatusCodes.Contains(details.HttpStatusCode.Value))
			return response;

		// If the factory already extracted a product error, skip body deserialization
		// (the error is already available via ApiCallDetails.ProductError).
		if (details.ProductError?.HasError() == true)
			return response;

		var beforeTicks = Stopwatch.GetTimestamp();

		response = isAsync
			? await boundConfiguration.ConnectionSettings.RequestResponseSerializer.DeserializeAsync<TResponse>(responseStream, cancellationToken).ConfigureAwait(false)
			: boundConfiguration.ConnectionSettings.RequestResponseSerializer.Deserialize<TResponse>(responseStream);

		var deserializeResponseMs = (Stopwatch.GetTimestamp() - beforeTicks) / (Stopwatch.Frequency / 1000);

		if (deserializeResponseMs > OpenTelemetry.MinimumMillisecondsToEmitTimingSpanAttribute && OpenTelemetry.CurrentSpanIsElasticTransportOwnedHasListenersAndAllDataRequested)
			_ = Activity.Current?.SetTag(OpenTelemetryAttributes.ElasticTransportDeserializeResponseMs, deserializeResponseMs);

		return response;
	}
}
