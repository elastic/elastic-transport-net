using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Elastic.Transport.Extensions;

namespace Elastic.Transport
{
	/// <summary>
	///     A helper class that deals with handling how a <see cref="Stream" /> is transformed to the requested
	///     <see cref="ITransportResponse" /> implementation. This includes handling optionally buffering based on
	///     <see cref="ITransportConfiguration.DisableDirectStreaming" />. And handling short circuiting special responses
	///     such as <see cref="StringResponse" />, <see cref="BytesResponse" /> and <see cref="VoidResponse" />
	/// </summary>
	public static class ResponseBuilder
	{
		private const int BufferSize = 81920;

		private static readonly Type[] SpecialTypes =
		{
			typeof(StringResponse), typeof(BytesResponse), typeof(VoidResponse), typeof(DynamicResponse)
		};

		/// <summary>
		///     A helper which returns true if the response could potentially have a body.
		/// </summary>
		private static bool MayHaveBody(int? statusCode, HttpMethod httpMethod) =>
			!statusCode.HasValue || statusCode.Value != 204 && httpMethod != HttpMethod.HEAD;

		/// <summary>
		///     Create an instance of <typeparamref name="TResponse" /> from <paramref name="responseStream" />
		/// </summary>
		public static TResponse ToResponse<TResponse>(
			RequestData requestData,
			Exception ex,
			int? statusCode,
			Dictionary<string, IEnumerable<string>> headers,
			Stream responseStream,
			string mimeType = RequestData.MimeType
		)
			where TResponse : class, ITransportResponse, new()
		{
			responseStream.ThrowIfNull(nameof(responseStream));

			var details = Initialize(requestData, ex, statusCode, headers, mimeType);

			TResponse response = null;
			// Only attempt to set the body if the response may have content
			if (MayHaveBody(statusCode, requestData.Method))
				response = SetBody<TResponse>(details, requestData, responseStream, mimeType);
			else
				responseStream.Dispose();

			response ??= new TResponse();

			response.ApiCall = details;
			return response;
		}

		/// <summary>
		///     Create an instance of <typeparamref name="TResponse" /> from <paramref name="responseStream" />
		/// </summary>
		public static async Task<TResponse> ToResponseAsync<TResponse>(
			RequestData requestData,
			Exception ex,
			int? statusCode,
			Dictionary<string, IEnumerable<string>> headers,
			Stream responseStream,
			string mimeType = RequestData.MimeType,
			CancellationToken cancellationToken = default
		)
			where TResponse : class, ITransportResponse, new()
		{
			responseStream.ThrowIfNull(nameof(responseStream));

			var details = Initialize(requestData, ex, statusCode, headers, mimeType);

			TResponse response = null;

			// Only attempt to set the body if the response may have content
			if (MayHaveBody(statusCode, requestData.Method))
				response = await SetBodyAsync<TResponse>(details, requestData, responseStream, mimeType,
					cancellationToken).ConfigureAwait(false);
			else
				responseStream.Dispose();

			response ??= new TResponse();

			response.ApiCall = details;
			return response;
		}

		private static ApiCallDetails Initialize(
			RequestData requestData, Exception exception, int? statusCode, Dictionary<string, IEnumerable<string>> headers, string mimeType
		)
		{
			var success = false;
			var allowedStatusCodes = requestData.AllowedStatusCodes;
			if (statusCode.HasValue)
			{
				if (allowedStatusCodes.Contains(-1) || allowedStatusCodes.Contains(statusCode.Value))
					success = true;
				else
					success = requestData.ConnectionSettings
						.StatusCodeToResponseSuccess(requestData.Method, statusCode.Value);
			}

			//mimeType can include charset information on .NET full framework
			if (!string.IsNullOrEmpty(mimeType) && !mimeType.StartsWith(requestData.Accept))
				success = false;

			var details = new ApiCallDetails
			{
				Success = success,
				OriginalException = exception,
				HttpStatusCode = statusCode,
				RequestBodyInBytes = requestData.PostData?.WrittenBytes,
				Uri = requestData.Uri,
				HttpMethod = requestData.Method,
				//DeprecationWarnings = warnings ?? Enumerable.Empty<string>(),
				ResponseMimeType = mimeType,
				ConnectionConfiguration = requestData.ConnectionSettings
			};

			if (headers is not null)
				details.ParsedHeaders = new ReadOnlyDictionary<string, IEnumerable<string>>(headers);

			return details;
		}

		private static TResponse SetBody<TResponse>(ApiCallDetails details, RequestData requestData,
			Stream responseStream, string mimeType)
			where TResponse : class, ITransportResponse, new()
		{
			byte[] bytes = null;
			var disableDirectStreaming = requestData.PostData?.DisableDirectStreaming ??
			                             requestData.ConnectionSettings.DisableDirectStreaming;
			if (disableDirectStreaming || NeedsToEagerReadStream<TResponse>())
			{
				var inMemoryStream = requestData.MemoryStreamFactory.Create();
				responseStream.CopyTo(inMemoryStream, BufferSize);
				bytes = SwapStreams(ref responseStream, ref inMemoryStream);
				details.ResponseBodyInBytes = bytes;
			}

			using (responseStream)
			{
				if (SetSpecialTypes<TResponse>(mimeType, bytes, requestData.MemoryStreamFactory, out var r))
					return r;

				if (details.HttpStatusCode.HasValue &&
				    requestData.SkipDeserializationForStatusCodes.Contains(details.HttpStatusCode.Value))
					return null;

				var serializer = requestData.ConnectionSettings.RequestResponseSerializer;
				if (requestData.CustomResponseBuilder != null)
					return requestData.CustomResponseBuilder.DeserializeResponse(serializer, details, responseStream) as
						TResponse;

				return mimeType == null || !mimeType.StartsWith(requestData.Accept, StringComparison.Ordinal)
					? null
					: serializer.Deserialize<TResponse>(responseStream);
			}
		}

		private static async Task<TResponse> SetBodyAsync<TResponse>(
			ApiCallDetails details, RequestData requestData, Stream responseStream, string mimeType,
			CancellationToken cancellationToken
		)
			where TResponse : class, ITransportResponse, new()
		{
			byte[] bytes = null;
			var disableDirectStreaming = requestData.PostData?.DisableDirectStreaming ??
			                             requestData.ConnectionSettings.DisableDirectStreaming;
			if (disableDirectStreaming || NeedsToEagerReadStream<TResponse>())
			{
				var inMemoryStream = requestData.MemoryStreamFactory.Create();
				await responseStream.CopyToAsync(inMemoryStream, BufferSize, cancellationToken).ConfigureAwait(false);
				bytes = SwapStreams(ref responseStream, ref inMemoryStream);
				details.ResponseBodyInBytes = bytes;
			}

			using (responseStream)
			{
				if (SetSpecialTypes<TResponse>(mimeType, bytes, requestData.MemoryStreamFactory, out var r)) return r;

				if (details.HttpStatusCode.HasValue &&
				    requestData.SkipDeserializationForStatusCodes.Contains(details.HttpStatusCode.Value))
					return null;

				var serializer = requestData.ConnectionSettings.RequestResponseSerializer;
				if (requestData.CustomResponseBuilder != null)
					return await requestData.CustomResponseBuilder
						.DeserializeResponseAsync(serializer, details, responseStream, cancellationToken)
						.ConfigureAwait(false) as TResponse;

				// TODO: Handle empty data in a nicer way as throwing exceptions has a cost we'd like to avoid!
				// ie. check content-length (add to ApiCallDetails)?
				try
				{
					return mimeType == null || !mimeType.StartsWith(requestData.Accept, StringComparison.Ordinal)
						? null
						: await serializer
							.DeserializeAsync<TResponse>(responseStream, cancellationToken)
							.ConfigureAwait(false);
				}
				catch (JsonException ex) when (ex.Message.Contains("The input does not contain any JSON tokens"))
				{
					return default;
				}
			}
		}

		private static bool SetSpecialTypes<TResponse>(string mimeType, byte[] bytes,
			IMemoryStreamFactory memoryStreamFactory, out TResponse cs)
			where TResponse : class, ITransportResponse, new()
		{
			cs = null;
			var responseType = typeof(TResponse);
			if (!SpecialTypes.Contains(responseType)) return false;

			if (responseType == typeof(StringResponse))
				cs = new StringResponse(bytes.Utf8String()) as TResponse;
			else if (responseType == typeof(BytesResponse))
				cs = new BytesResponse(bytes) as TResponse;
			else if (responseType == typeof(VoidResponse))
				cs = VoidResponse.Default as TResponse;
			else if (responseType == typeof(DynamicResponse))
			{
				//if not json store the result under "body"
				if (mimeType == null || !mimeType.StartsWith(RequestData.MimeType))
				{
					var dictionary = new DynamicDictionary
					{
						["body"] = new DynamicValue(bytes.Utf8String())
					};
					cs = new DynamicResponse(dictionary) as TResponse;
				}
				else
				{
					using var ms = memoryStreamFactory.Create(bytes);
					var body = LowLevelRequestResponseSerializer.Instance.Deserialize<DynamicDictionary>(ms);
					cs = new DynamicResponse(body) as TResponse;
				}
			}

			return cs != null;
		}

		private static bool NeedsToEagerReadStream<TResponse>()
			where TResponse : class, ITransportResponse, new() =>
			typeof(TResponse) == typeof(StringResponse)
			|| typeof(TResponse) == typeof(BytesResponse)
			|| typeof(TResponse) == typeof(DynamicResponse);

		private static byte[] SwapStreams(ref Stream responseStream, ref MemoryStream ms)
		{
			var bytes = ms.ToArray();
			responseStream.Dispose();
			responseStream = ms;
			responseStream.Position = 0;
			return bytes;
		}
	}
}
