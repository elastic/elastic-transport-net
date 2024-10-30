// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Elastic.Transport;

/// <summary>
/// An implementation of <see cref="IRequestInvoker"/> designed to not actually do any IO and services requests from an in memory byte buffer
/// </summary>
public class InMemoryRequestInvoker : IRequestInvoker
{
	private static readonly byte[] EmptyBody = Encoding.UTF8.GetBytes("");
	private readonly string _contentType;
	private readonly Exception? _exception;
	private readonly byte[] _responseBody;
	private readonly int _statusCode;
	private readonly Dictionary<string, IEnumerable<string>> _headers;

	/// <summary>
	/// Every request will succeed with this overload, note that it won't actually return mocked responses
	/// so using this overload might fail if you are using it to test high level bits that need to deserialize the response.
	/// </summary>
	public InMemoryRequestInvoker() => _statusCode = 200;

	/// <inheritdoc cref="InMemoryRequestInvoker"/>
	public InMemoryRequestInvoker(byte[] responseBody, int statusCode = 200, Exception? exception = null, string contentType = RequestData.DefaultMimeType, Dictionary<string, IEnumerable<string>> headers = null)
	{
		_responseBody = responseBody;
		_statusCode = statusCode;
		_exception = exception;
		_contentType = contentType;
		_headers = headers;
	}

	void IDisposable.Dispose() { }

	/// <inheritdoc cref="IRequestInvoker.Request{TResponse}"/>>
	public TResponse Request<TResponse>(Endpoint endpoint, RequestData requestData, PostData? postData)
		where TResponse : TransportResponse, new() =>
		BuildResponse<TResponse>(endpoint, requestData, postData);

	/// <inheritdoc cref="IRequestInvoker.RequestAsync{TResponse}"/>>
	public Task<TResponse> RequestAsync<TResponse>(Endpoint endpoint, RequestData requestData, PostData? postData, CancellationToken cancellationToken)
		where TResponse : TransportResponse, new() =>
		BuildResponseAsync<TResponse>(endpoint, requestData, postData, cancellationToken);

	/// <summary>
	/// Allow subclasses to provide their own implementations for <see cref="IRequestInvoker.Request{TResponse}"/> while reusing the more complex logic
	/// to create a response
	/// </summary>
	/// <param name="endpoint">An instance of <see cref="Endpoint"/> describing where to call out to</param>
	/// <param name="requestData">An instance of <see cref="RequestData"/> describing how to call out to</param>
	/// <param name="postData">Optional data to post</param>
	/// <param name="responseBody">The bytes intended to be used as return</param>
	/// <param name="statusCode">The status code that the responses <see cref="TransportResponse.ApiCallDetails"/> should return</param>
	/// <param name="contentType"></param>
	public TResponse BuildResponse<TResponse>(Endpoint endpoint, RequestData requestData, PostData? postData, byte[]? responseBody = null, int? statusCode = null,
		string? contentType = null)
		where TResponse : TransportResponse, new()
	{
		var body = responseBody ?? _responseBody;
		var data = postData;

		if (data is not null)
		{
			using var stream = requestData.MemoryStreamFactory.Create();
			if (requestData.HttpCompression)
			{
				using var zipStream = new GZipStream(stream, CompressionMode.Compress);
				data.Write(zipStream, requestData.ConnectionSettings);
			}
			else
			{
				data.Write(stream, requestData.ConnectionSettings);
			}
		}

		var sc = statusCode ?? _statusCode;
		Stream responseStream = body != null ? requestData.MemoryStreamFactory.Create(body) : requestData.MemoryStreamFactory.Create(EmptyBody);

		return requestData.ConnectionSettings.ProductRegistration.ResponseBuilder
			.ToResponse<TResponse>(endpoint, requestData, postData, _exception, sc, _headers, responseStream, contentType ?? _contentType ?? RequestData.DefaultMimeType, body?.Length ?? 0, null, null);
	}

	/// <inheritdoc cref="BuildResponse{TResponse}"/>>
	public async Task<TResponse> BuildResponseAsync<TResponse>(Endpoint endpoint, RequestData requestData, PostData? postData, CancellationToken cancellationToken,
		byte[]? responseBody = null, int? statusCode = null, string? contentType = null)
		where TResponse : TransportResponse, new()
	{
		var body = responseBody ?? _responseBody;
		var data = postData;

		if (data is not null)
		{
			using var stream = requestData.MemoryStreamFactory.Create();

			if (requestData.HttpCompression)
			{
				using var zipStream = new GZipStream(stream, CompressionMode.Compress);
				await data.WriteAsync(zipStream, requestData.ConnectionSettings, cancellationToken).ConfigureAwait(false);
			}
			else
			{
				await data.WriteAsync(stream, requestData.ConnectionSettings, cancellationToken).ConfigureAwait(false);
			}
		}
		var sc = statusCode ?? _statusCode;

		Stream responseStream = body != null ? requestData.MemoryStreamFactory.Create(body) : requestData.MemoryStreamFactory.Create(EmptyBody);

		return await requestData.ConnectionSettings.ProductRegistration.ResponseBuilder
			.ToResponseAsync<TResponse>(endpoint, requestData, postData, _exception, sc, _headers, responseStream, contentType ?? _contentType, body?.Length ?? 0, null, null, cancellationToken)
			.ConfigureAwait(false);
	}
}
