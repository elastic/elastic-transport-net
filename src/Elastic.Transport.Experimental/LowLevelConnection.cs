using System;
using System.Buffers;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using NetworkToolkit.Connections;
using NetworkToolkit.Http.Primitives;
using static System.Text.Encoding;

namespace Elastic.Transport.Experimental
{
	/// <summary>
	/// 
	/// </summary>
	public class LowLevelConnection : IConnection
	{
		private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true, Converters = { new JsonStringEnumConverter() }};
		private readonly SocketConnectionFactory _socketConnectionFactory;
		
		/// <summary>
		/// 
		/// </summary>
		public LowLevelConnection() => _socketConnectionFactory = new SocketConnectionFactory();
		
		/// <summary>
		/// 
		/// </summary>
		/// <param name="requestData"></param>
		/// <param name="cancellationToken"></param>
		/// <typeparam name="TResponse"></typeparam>
		/// <returns></returns>
		public async Task<TResponse> RequestAsync<TResponse>(RequestData requestData, CancellationToken cancellationToken) where TResponse : class, ITransportResponse, new()
		{
			// This code is temporary
			// We need to support proper connection pooling and re-use with DNS timeouts etc.
			// Right now we're tying up ports in TIME_WAIT
			await using var connection = await _socketConnectionFactory.ConnectAsync(new DnsEndPoint(requestData.Uri.Host, requestData.Uri.Port), cancellationToken: cancellationToken).ConfigureAwait(false);
			await using var httpConnection = new Http1Connection(connection, HttpPrimitiveVersion.Version11);

			await using var valueRequest = await httpConnection.CreateNewRequestAsync(HttpPrimitiveVersion.Version11, HttpVersionPolicy.RequestVersionExact, cancellationToken).ConfigureAwait(false);

			if (!valueRequest.HasValue) return default;

			var request = valueRequest.Value;
			
			// TODO - This is just hardcoded for a GET request right now!!
			// We should be able to avoid most of these byte[] and string allocations.

			request.ConfigureRequest(contentLength: 0, hasTrailingHeaders: false);
			request.WriteRequest(ASCII.GetBytes("GET"), ASCII.GetBytes(requestData.Uri.Authority), ASCII.GetBytes(requestData.Uri.PathAndQuery)); // TODO: avoid allocations
			request.WriteHeader("Accept", "application/json"); // TODO: Don't hard code this. Consider Prepared Headers

			await request.CompleteRequestAsync(cancellationToken).ConfigureAwait(false);

			var sink = new ContentLengthSink(); // NOTE: Not a huge fan of this way of getting at headers
			// Could we perhaps pass in a buffer to a new method, request.ReadNextHeaderName(ReadOnlySpan<byte> buffer) and handle the bytes without the sink class?
			if (await request.ReadToHeadersAsync(cancellationToken).ConfigureAwait(false))
			{
				// TODO: Could also provide state to be set by this
				await request.ReadHeadersAsync(sink, null, cancellationToken).ConfigureAwait(false);
			}

			await request.ReadToFinalResponseAsync(cancellationToken).ConfigureAwait(false);

			TResponse response = default;
			var statusCode = request.StatusCode;
			
			if (statusCode == HttpStatusCode.OK && await request.ReadToContentAsync(cancellationToken).ConfigureAwait(false))
			{
				// This is hacky right now. For very large responses we should read in chunks.
				// Need to consider handling deserialization in that case and perhaps use Utf8JsonReader.
				// Consider how to support custom readers for response types taking ROS<byte>.

				var buffer = ArrayPool<byte>.Shared.Rent(sink.ContentLength);
				
				try
				{
					var length = await request.ReadContentAsync(buffer, cancellationToken).ConfigureAwait(false); // Question: Is the response fully pre-buffered or might it still be streaming?
					response = JsonSerializer.Deserialize<TResponse>(buffer.AsSpan(0, length), _jsonOptions);
				}
				catch (Exception ex)
				{
					Console.WriteLine(ex);
				}
				finally
				{
					ArrayPool<byte>.Shared.Return(buffer);
				}
			}
			
			await request.DrainAsync(cancellationToken).ConfigureAwait(false);

			response ??= new TResponse();

			// Temporary as we don't properly initialize this right now.
			// TODO: Could ApiCallDetails be made opt-in such that we only allocate if the consumer wants it?
			response.ApiCall = new ApiCallDetails{ HttpMethod = requestData.Method, HttpStatusCode = (int)statusCode, Uri = requestData.Uri };
			
			return response;
		}
		
		/// <summary>
		/// 
		/// </summary>
		/// <param name="requestData"></param>
		/// <typeparam name="TResponse"></typeparam>
		/// <returns></returns>
		public TResponse Request<TResponse>(RequestData requestData) where TResponse : class, ITransportResponse, new() => throw new NotImplementedException();

		/// <summary>
		/// 
		/// </summary>
		public void Dispose() => Task.Factory.StartNew(async () => await _socketConnectionFactory.DisposeAsync().ConfigureAwait(false)); // TODO: IConnection needs to support IAsyncDisposable
	}

	internal class ContentLengthSink : IHttpHeadersSink
	{
		private static ReadOnlySpan<byte> ContentLengthSpan => ASCII.GetBytes("content-length"); // Temp

		public int ContentLength { get; private set; }

		public void OnHeader(object state, ReadOnlySpan<byte> headerName, ReadOnlySpan<byte> headerValue)
		{
			if (headerName.SequenceEqual(ContentLengthSpan)) // TODO: Consider case
			{
				var chars = ArrayPool<char>.Shared.Rent(headerValue.Length);

				try
				{
					var charCount = ASCII.GetChars(headerValue, chars);

					if (int.TryParse(chars.AsSpan().Slice(0, charCount), out var contentLength))
					{
						ContentLength = contentLength;
					}
				}
				finally
				{
					ArrayPool<char>.Shared.Return(chars);
				}
			}
		}
	}
}
