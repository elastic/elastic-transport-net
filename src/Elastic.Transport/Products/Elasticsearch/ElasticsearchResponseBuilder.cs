// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using Elastic.Transport.Diagnostics;
using System.Threading.Tasks;
using System.Threading;
using System;

namespace Elastic.Transport.Products.Elasticsearch;

internal sealed class ElasticsearchResponseBuilder : IResponseBuilder
{
	bool IResponseBuilder.CanBuild<TResponse>() => true;

	public TResponse Build<TResponse>(ApiCallDetails apiCallDetails, BoundConfiguration boundConfiguration,
		Stream responseStream, string contentType, long contentLength)
		where TResponse : TransportResponse, new() =>
			SetBodyCoreAsync<TResponse>(false, apiCallDetails, boundConfiguration, responseStream).EnsureCompleted();

	public Task<TResponse> BuildAsync<TResponse>(
		ApiCallDetails apiCallDetails, BoundConfiguration boundConfiguration, Stream responseStream, string contentType, long contentLength,
		CancellationToken cancellationToken) where TResponse : TransportResponse, new() =>
			SetBodyCoreAsync<TResponse>(true, apiCallDetails, boundConfiguration, responseStream, cancellationToken).AsTask();

	private static async ValueTask<TResponse> SetBodyCoreAsync<TResponse>(bool isAsync,
		ApiCallDetails details, BoundConfiguration boundConfiguration, Stream responseStream,
		CancellationToken cancellationToken = default)
		where TResponse : TransportResponse, new()
	{
		TResponse response = null;

		if (details.HttpStatusCode.HasValue &&
			boundConfiguration.SkipDeserializationForStatusCodes.Contains(details.HttpStatusCode.Value))
		{
			return response;
		}

		try
		{
			if (details.HttpStatusCode > 399)
			{
				var ownsStream = false;

				if (!responseStream.CanSeek)
				{
					var inMemoryStream = boundConfiguration.MemoryStreamFactory.Create();
					await responseStream.CopyToAsync(inMemoryStream, BufferedResponseHelpers.BufferSize, cancellationToken).ConfigureAwait(false);
					details.ResponseBodyInBytes = BufferedResponseHelpers.SwapStreams(ref responseStream, ref inMemoryStream);
					ownsStream = true;
				}

				if (TryGetError(boundConfiguration, responseStream, out var error) && error.HasError())
				{
					response = new TResponse();

					if (response is ElasticsearchResponse elasticResponse)
						elasticResponse.ElasticsearchServerError = error;

					if (ownsStream)
						responseStream.Dispose();

					return response;
				}

				responseStream.Position = 0;
			}

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

	private static bool TryGetError(BoundConfiguration boundConfiguration, Stream responseStream, out ElasticsearchServerError error)
	{
		Debug.Assert(responseStream.CanSeek);

		error = null;

		try
		{
			error = boundConfiguration.ConnectionSettings.RequestResponseSerializer.Deserialize<ElasticsearchServerError>(responseStream);
			return error is not null;
		}
		catch (JsonException)
		{
			// Empty catch as we'll try the original response type if the error serialization fails
		}

		return false;
	}
}
