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

namespace Elastic.Transport.Products.Elasticsearch;

/// <summary>
/// Catch-all builder for <see cref="ElasticsearchResponse"/> subclasses.
/// Handles error extraction and JSON deserialization for strongly-typed response types.
/// <para>
/// The individual <see cref="IElasticsearchResponse"/> native types (e.g. <see cref="ElasticsearchStringResponse"/>)
/// are handled by their own builders registered in <see cref="ElasticsearchProductRegistration.ResponseBuilders"/>
/// via <see cref="ElasticsearchErrorDecorator{T}"/>.
/// </para>
/// </summary>
internal sealed class ElasticsearchResponseBuilder : IResponseBuilder
{
	bool IResponseBuilder.CanBuild<TResponse>() => true;

	public TResponse? Build<TResponse>(ApiCallDetails apiCallDetails, BoundConfiguration boundConfiguration,
		Stream responseStream, string contentType, long contentLength)
		where TResponse : TransportResponse, new() =>
			SetBodyCoreAsync<TResponse>(false, apiCallDetails, boundConfiguration, responseStream, contentType, contentLength).EnsureCompleted();

	public Task<TResponse?> BuildAsync<TResponse>(
		ApiCallDetails apiCallDetails, BoundConfiguration boundConfiguration, Stream responseStream, string contentType, long contentLength,
		CancellationToken cancellationToken) where TResponse : TransportResponse, new() =>
			SetBodyCoreAsync<TResponse>(true, apiCallDetails, boundConfiguration, responseStream, contentType, contentLength, cancellationToken).AsTask();

	private static async ValueTask<TResponse?> SetBodyCoreAsync<TResponse>(bool isAsync,
		ApiCallDetails details, BoundConfiguration boundConfiguration, Stream responseStream,
		string contentType, long contentLength,
		CancellationToken cancellationToken = default)
		where TResponse : TransportResponse, new()
	{
		TResponse? response = null;

		if (details.HttpStatusCode.HasValue &&
			boundConfiguration.SkipDeserializationForStatusCodes.Contains(details.HttpStatusCode.Value))
		{
			return response;
		}

		try
		{
			ElasticsearchServerError? error = null;
			var ownsStream = false;

			if (details.HttpStatusCode > 399)
			{
				if (!responseStream.CanSeek)
				{
					var inMemoryStream = boundConfiguration.MemoryStreamFactory.Create();
					await responseStream.CopyToAsync(inMemoryStream, BufferedResponseHelpers.BufferSize, cancellationToken).ConfigureAwait(false);
					details.ResponseBodyInBytes = BufferedResponseHelpers.SwapStreams(ref responseStream, ref inMemoryStream);
					ownsStream = true;
				}

				ElasticsearchErrorHelper.TryGetError(boundConfiguration, responseStream, out error);
				responseStream.Position = 0;
			}

			if (error?.HasError() == true)
			{
				response = new TResponse();

				if (response is ElasticsearchResponse elasticResponse)
					elasticResponse.ElasticsearchServerError = error;

				if (ownsStream)
					responseStream.Dispose();

				return response;
			}

			var beforeTicks = Stopwatch.GetTimestamp();

			response = isAsync
				? await boundConfiguration.ConnectionSettings.RequestResponseSerializer.DeserializeAsync<TResponse>(responseStream, cancellationToken).ConfigureAwait(false)
				: boundConfiguration.ConnectionSettings.RequestResponseSerializer.Deserialize<TResponse>(responseStream);

			var deserializeResponseMs = (Stopwatch.GetTimestamp() - beforeTicks) / (Stopwatch.Frequency / 1000);

			if (deserializeResponseMs > OpenTelemetry.MinimumMillisecondsToEmitTimingSpanAttribute && OpenTelemetry.CurrentSpanIsElasticTransportOwnedHasListenersAndAllDataRequested)
				_ = (Activity.Current?.SetTag(OpenTelemetryAttributes.ElasticTransportDeserializeResponseMs, deserializeResponseMs));

			if (ownsStream)
				responseStream.Dispose();

			return response;
		}
		catch (JsonException ex) when (ex.Message.Contains("The input does not contain any JSON tokens"))
		{
			return response;
		}
	}
}
