// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

#if !NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using Elastic.Transport.Diagnostics;
using Elastic.Transport.Extensions;
using static System.Net.DecompressionMethods;

namespace Elastic.Transport;

/// <summary>
/// The default <see cref="IRequestInvoker"/> implementation. Uses <see cref="HttpClient" /> to make requests.
/// </summary>
public class HttpRequestInvoker : IRequestInvoker
{
	private static readonly string MissingConnectionLimitMethodError =
		$"Your target platform does not support {nameof(TransportConfigurationDescriptor.ConnectionLimit)}"
		+ $" please set {nameof(TransportConfigurationDescriptor.ConnectionLimit)} to -1 on your configuration."
		+ $" this will cause the {nameof(HttpClientHandler.MaxConnectionsPerServer)} not to be set on {nameof(HttpClientHandler)}";

	private string _expectedCertificateFingerprint;

	/// <summary>
	/// Create a new instance of the <see cref="HttpRequestInvoker"/>.
	/// </summary>
	public HttpRequestInvoker() : this(new DefaultResponseFactory()) { }

	internal HttpRequestInvoker(ResponseFactory responseFactory)
	{
		ResponseFactory = responseFactory;
		HttpClientFactory = new RequestDataHttpClientFactory(CreateHttpClientHandler);
	}

	/// <summary>
	/// Allows consumers to inject their own HttpMessageHandler, and optionally call our default implementation.
	/// </summary>
	public HttpRequestInvoker(Func<HttpMessageHandler, RequestData, HttpMessageHandler> wrappingHandler) :
		this(wrappingHandler, new DefaultResponseFactory()) { }

	/// <summary>
	/// Allows consumers to inject their own HttpMessageHandler, and optionally call our default implementation.
	/// </summary>
	public HttpRequestInvoker(Func<HttpMessageHandler, RequestData, HttpMessageHandler> wrappingHandler, ITransportConfiguration transportConfiguration) :
		this(wrappingHandler, new DefaultResponseFactory())
	{ }

	internal HttpRequestInvoker(Func<HttpMessageHandler, RequestData, HttpMessageHandler> wrappingHandler, ResponseFactory responseFactory)
	{
		ResponseFactory = responseFactory;
		HttpClientFactory = new RequestDataHttpClientFactory(r =>
		{
			var defaultHandler = CreateHttpClientHandler(r);
			return wrappingHandler(defaultHandler, r) ?? defaultHandler;
		});
	}

	/// <inheritdoc />
	public ResponseFactory ResponseFactory { get; }

	/// <inheritdoc cref="RequestDataHttpClientFactory.InUseHandlers" />
	public int InUseHandlers => HttpClientFactory.InUseHandlers;

	/// <inheritdoc cref="RequestDataHttpClientFactory.RemovedHandlers" />
	public int RemovedHandlers => HttpClientFactory.RemovedHandlers;

	private static DiagnosticSource DiagnosticSource { get; } = new DiagnosticListener(DiagnosticSources.HttpConnection.SourceName);

	private RequestDataHttpClientFactory HttpClientFactory { get; }

	/// <inheritdoc cref="IRequestInvoker.Request{TResponse}" />
	public TResponse Request<TResponse>(Endpoint endpoint, RequestData requestData, PostData? postData)
		where TResponse : TransportResponse, new() =>
		RequestCoreAsync<TResponse>(false, endpoint, requestData, postData).EnsureCompleted();

	/// <inheritdoc cref="IRequestInvoker.RequestAsync{TResponse}" />
	public Task<TResponse> RequestAsync<TResponse>(Endpoint endpoint, RequestData requestData, PostData? postData, CancellationToken cancellationToken)
		where TResponse : TransportResponse, new() =>
		RequestCoreAsync<TResponse>(true, endpoint, requestData, postData, cancellationToken).AsTask();

	private async ValueTask<TResponse> RequestCoreAsync<TResponse>(bool isAsync, Endpoint endpoint, RequestData requestData, PostData? postData, CancellationToken cancellationToken = default)
		where TResponse : TransportResponse, new()
	{
		var client = GetClient(requestData);
		HttpResponseMessage responseMessage;
		int? statusCode = null;
		Stream responseStream = null;
		Exception ex = null;
		string contentType = null;
		long contentLength = -1;
		IDisposable receivedResponse = DiagnosticSources.SingletonDisposable;
		ReadOnlyDictionary<TcpState, int> tcpStats = null;
		ReadOnlyDictionary<string, ThreadPoolStatistics> threadPoolStats = null;
		Dictionary<string, IEnumerable<string>> responseHeaders = null;

		var beforeTicks = Stopwatch.GetTimestamp();

		try
		{
			var requestMessage = CreateHttpRequestMessage(endpoint, requestData, isAsync);

			if (postData is not null)
			{
				if (isAsync)
					await SetContentAsync(requestMessage, requestData, postData, cancellationToken).ConfigureAwait(false);
				else
					SetContent(requestMessage, requestData, postData);
			}

			using (requestMessage?.Content ?? (IDisposable)Stream.Null)
			{
				if (requestData.EnableTcpStats)
					tcpStats = TcpStats.GetStates();

				if (requestData.EnableThreadPoolStats)
					threadPoolStats = ThreadPoolStats.GetStats();

				var prepareRequestMs = (Stopwatch.GetTimestamp() - beforeTicks) / (Stopwatch.Frequency / 1000);

				if (prepareRequestMs > OpenTelemetry.MinimumMillisecondsToEmitTimingSpanAttribute && OpenTelemetry.CurrentSpanIsElasticTransportOwnedHasListenersAndAllDataRequested)
					Activity.Current?.SetTag(OpenTelemetryAttributes.ElasticTransportPrepareRequestMs, prepareRequestMs);

				if (isAsync)
					responseMessage = await client.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
						.ConfigureAwait(false);
				else
#if NET6_0_OR_GREATER
					responseMessage = client.Send(requestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
#else
					responseMessage = client.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken).GetAwaiter().GetResult();
#endif

				receivedResponse = responseMessage;
				statusCode = (int)responseMessage.StatusCode;
			}

			contentType = responseMessage.Content.Headers.ContentType?.ToString();
			responseHeaders = ParseHeaders(requestData, responseMessage);

			if (responseMessage.Content != null)
			{
				if (isAsync)
#if NET6_0_OR_GREATER
					responseStream = await responseMessage.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
#else
					responseStream = await responseMessage.Content.ReadAsStreamAsync().ConfigureAwait(false);
#endif
				else
#if NET6_0_OR_GREATER
					responseStream = responseMessage.Content.ReadAsStream(cancellationToken);
#else
					responseStream = responseMessage.Content.ReadAsStreamAsync().GetAwaiter().GetResult();
#endif
			}

			// We often won't have the content length as most responses are GZip compressed and the HttpContent ditches this when AutomaticDecompression is enabled.
			contentLength = responseMessage.Content.Headers.ContentLength ?? -1;
		}
		catch (TaskCanceledException e)
		{
			ex = e;
		}
		catch (HttpRequestException e)
		{
			ex = e;
		}

		TResponse response;

		try
		{
			if (isAsync)
				response = await ResponseFactory.CreateAsync<TResponse>
					(endpoint, requestData, postData, ex, statusCode, responseHeaders, responseStream, contentType, contentLength, threadPoolStats, tcpStats, cancellationToken)
						.ConfigureAwait(false);
			else
				response = ResponseFactory.Create<TResponse>
						(endpoint, requestData, postData, ex, statusCode, responseHeaders, responseStream, contentType, contentLength, threadPoolStats, tcpStats);

			// Unless indicated otherwise by the TransportResponse, we've now handled the response stream, so we can dispose of the HttpResponseMessage
			// to release the connection. In cases, where the derived response works directly on the stream, it can be left open and additional IDisposable
			// resources can be linked such that their disposal is deferred.
			if (response.LeaveOpen)
			{
				response.LinkedDisposables = [receivedResponse, responseStream];
			}
			else
			{
				responseStream.Dispose();
				receivedResponse.Dispose();
			}

			if (!OpenTelemetry.CurrentSpanIsElasticTransportOwnedAndHasListeners || (!(Activity.Current?.IsAllDataRequested ?? false)))
				return response;

			var attributes = requestData.ConnectionSettings.ProductRegistration.ParseOpenTelemetryAttributesFromApiCallDetails(response.ApiCallDetails);

			if (attributes is null) return response;

			foreach (var attribute in attributes)
				Activity.Current?.SetTag(attribute.Key, attribute.Value);

			return response;
		}
		catch
		{
			// if there's an exception, ensure we always release the stream and response so that the connection is freed.
			responseStream.Dispose();
			receivedResponse.Dispose();
			throw;
		}
	}

	private static Dictionary<string, IEnumerable<string>>? ParseHeaders(RequestData requestData, HttpResponseMessage responseMessage)
	{
		Dictionary<string, IEnumerable<string>>? responseHeaders = null;
		var defaultHeadersForProduct = requestData.ConnectionSettings.ProductRegistration.DefaultHeadersToParse();
		foreach (var headerToParse in defaultHeadersForProduct)
			if (responseMessage.Headers.TryGetValues(headerToParse, out var values))
			{
				responseHeaders ??= new Dictionary<string, IEnumerable<string>>();
				responseHeaders.Add(headerToParse, values);
			}

		if (requestData.ParseAllHeaders)
			foreach (var header in responseMessage.Headers)
			{
				responseHeaders ??= new Dictionary<string, IEnumerable<string>>();
				responseHeaders.Add(header.Key, header.Value);
			}
		else if (requestData.ResponseHeadersToParse is { Count:  > 0 })
			foreach (var headerToParse in requestData.ResponseHeadersToParse)
				if (responseMessage.Headers.TryGetValues(headerToParse, out var values))
				{
					responseHeaders ??= new Dictionary<string, IEnumerable<string>>();
					responseHeaders.Add(headerToParse, values);
				}

		return responseHeaders;
	}

	private HttpClient GetClient(RequestData requestData) => HttpClientFactory.CreateClient(requestData);

	/// <summary>
	/// Creates an instance of <see cref="HttpMessageHandler" /> using the <paramref name="requestData" />.
	/// This method is virtual so subclasses of <see cref="HttpRequestInvoker" /> can modify the instance if needed.
	/// </summary>
	/// <param name="requestData">An instance of <see cref="RequestData" /> describing where and how to call out to</param>
	/// <exception cref="Exception">
	/// Can throw if <see cref="ITransportConfiguration.ConnectionLimit" /> is set but the platform does
	/// not allow this to be set on <see cref="HttpClientHandler.MaxConnectionsPerServer" />
	/// </exception>
	protected HttpMessageHandler CreateHttpClientHandler(RequestData requestData)
	{
		var handler = new HttpClientHandler { AutomaticDecompression = requestData.HttpCompression ? GZip | Deflate : None, };

		// same limit as desktop clr
		if (requestData.ConnectionSettings.ConnectionLimit > 0)
			try
			{
				handler.MaxConnectionsPerServer = requestData.ConnectionSettings.ConnectionLimit;
			}
			catch (MissingMethodException e)
			{
				throw new Exception(MissingConnectionLimitMethodError, e);
			}
			catch (PlatformNotSupportedException e)
			{
				throw new Exception(MissingConnectionLimitMethodError, e);
			}

		if (!requestData.ProxyAddress.IsNullOrEmpty())
		{
			var uri = new Uri(requestData.ProxyAddress);
			var proxy = new WebProxy(uri);
			if (!string.IsNullOrEmpty(requestData.ProxyUsername))
			{
				var credentials = new NetworkCredential(requestData.ProxyUsername, requestData.ProxyPassword);
				proxy.Credentials = credentials;
			}
			handler.Proxy = proxy;
		}
		else if (requestData.DisableAutomaticProxyDetection) handler.UseProxy = false;

		// Configure certificate validation
		var callback = requestData.ConnectionSettings?.ServerCertificateValidationCallback;
		if (callback != null && handler.ServerCertificateCustomValidationCallback == null)
		{
			handler.ServerCertificateCustomValidationCallback = callback;
		}
		else if (!string.IsNullOrEmpty(requestData.ConnectionSettings.CertificateFingerprint))
		{
			handler.ServerCertificateCustomValidationCallback = (request, certificate, chain, policyErrors) =>
			{
				if (certificate is null && chain is null) return false;

				// The "cleaned", expected fingerprint is cached to avoid repeated cost of converting it to a comparable form.
				_expectedCertificateFingerprint ??= CertificateHelpers.ComparableFingerprint(requestData.ConnectionSettings.CertificateFingerprint);

				// If there is a chain, check each certificate up to the root
				if (chain is not null)
				{
					foreach (var element in chain.ChainElements)
					{
						if (CertificateHelpers.ValidateCertificateFingerprint(element.Certificate, _expectedCertificateFingerprint))
							return true;
					}
				}

				// Otherwise, check the certificate
				return CertificateHelpers.ValidateCertificateFingerprint(certificate, _expectedCertificateFingerprint);
			};
		}

		if (requestData.ClientCertificates != null)
		{
			handler.ClientCertificateOptions = ClientCertificateOption.Manual;
			handler.ClientCertificates.AddRange(requestData.ClientCertificates);
		}

		return handler;
	}

	private string ComparableFingerprint(string fingerprint)
	{
		var finalFingerprint = fingerprint;

		if (fingerprint.Contains(':'))
			finalFingerprint = fingerprint.Replace(":", string.Empty);

		return finalFingerprint;
	}

	/// <summary>
	/// Creates an instance of <see cref="HttpRequestMessage" /> using the <paramref name="requestData" />.
	/// This method is virtual so subclasses of <see cref="HttpRequestInvoker" /> can modify the instance if needed.
	/// </summary>
	/// <param name="endpoint">An object describing where we want to call out to</param>
	/// <param name="requestData">An object describing how we want to call out to</param>
	/// <param name="isAsync"></param>
	/// <exception cref="Exception">
	/// Can throw if <see cref="ITransportConfiguration.ConnectionLimit" /> is set but the platform does
	/// not allow this to be set on <see cref="HttpClientHandler.MaxConnectionsPerServer" />
	/// </exception>
	internal HttpRequestMessage CreateHttpRequestMessage(Endpoint endpoint, RequestData requestData, bool isAsync)
	{
		var request = CreateRequestMessage(endpoint, requestData, isAsync);
		SetAuthenticationIfNeeded(endpoint, requestData, request);
		return request;
	}

	/// <summary> Isolated hook for subclasses to set authentication on <paramref name="requestMessage" /> </summary>
	/// <param name="requestMessage">The instance of <see cref="HttpRequestMessage" /> that needs authentication details</param>
	/// <param name="endpoint">An object describing where we want to call out to</param>
	/// <param name="requestData">An object describing how we want to call out to</param>
	internal void SetAuthenticationIfNeeded(Endpoint endpoint, RequestData requestData, HttpRequestMessage requestMessage)
	{
		//If user manually specifies an Authorization Header give it preference
		if (requestData.Headers != null && requestData.Headers.HasKeys() && requestData.Headers.AllKeys.Contains("Authorization"))
		{
			var header = AuthenticationHeaderValue.Parse(requestData.Headers["Authorization"]);
			requestMessage.Headers.Authorization = header;
			return;
		}

		SetConfiguredAuthenticationHeaderIfNeeded(endpoint, requestData, requestMessage);
	}

	private static void SetConfiguredAuthenticationHeaderIfNeeded(Endpoint endpoint, RequestData requestData, HttpRequestMessage requestMessage)
	{
		// Basic auth credentials take the following precedence (highest -> lowest):
		// 1 - Specified with the URI (highest precedence)
		// 2 - Specified on the request
		// 3 - Specified at the global TransportClientSettings level (lowest precedence)

		string parameters = null;
		string scheme = null;
		if (!endpoint.Uri.UserInfo.IsNullOrEmpty())
		{
			parameters = BasicAuthentication.GetBase64String(Uri.UnescapeDataString(endpoint.Uri.UserInfo));
			scheme = BasicAuthentication.BasicAuthenticationScheme;
		}
		else if (requestData.AuthenticationHeader != null && requestData.AuthenticationHeader.TryGetAuthorizationParameters(out var v))
		{
			parameters = v;
			scheme = requestData.AuthenticationHeader.AuthScheme;
		}

		if (parameters.IsNullOrEmpty()) return;

		requestMessage.Headers.Authorization = new AuthenticationHeaderValue(scheme, parameters);
	}

	private static HttpRequestMessage CreateRequestMessage(Endpoint endpoint, RequestData requestData, bool isAsync)
	{
		var method = ConvertHttpMethod(endpoint.Method);
		var requestMessage = new HttpRequestMessage(method, endpoint.Uri);

		if (requestData.Headers != null)
			foreach (string key in requestData.Headers)
				requestMessage.Headers.TryAddWithoutValidation(key, requestData.Headers.GetValues(key));

		requestMessage.Headers.Connection.Clear();
		requestMessage.Headers.ConnectionClose = false;
		requestMessage.Headers.TryAddWithoutValidation("Accept", requestData.Accept);

		var userAgent = requestData.UserAgent?.ToString();
		if (!string.IsNullOrWhiteSpace(userAgent))
		{
			requestMessage.Headers.UserAgent.Clear();
			requestMessage.Headers.UserAgent.TryParseAdd(userAgent);
		}

		if (!requestData.RunAs.IsNullOrEmpty())
			requestMessage.Headers.Add(RequestData.RunAsSecurityHeader, requestData.RunAs);

		if (requestData.MetaHeaderProvider is not null)
		{
			foreach (var producer in requestData.MetaHeaderProvider.Producers)
			{
				var value = producer.ProduceHeaderValue(requestData, isAsync);

				if (!string.IsNullOrEmpty(value))
					requestMessage.Headers.TryAddWithoutValidation(producer.HeaderName, value);
			}
		}

		return requestMessage;
	}

	private static void SetContent(HttpRequestMessage message, RequestData requestData, PostData postData)
	{
		if (requestData.TransferEncodingChunked)
			message.Content = new RequestDataContent(requestData, postData);
		else
		{
			var stream = requestData.MemoryStreamFactory.Create();
			if (requestData.HttpCompression)
			{
				using var zipStream = new GZipStream(stream, CompressionMode.Compress, true);
				postData.Write(zipStream, requestData.ConnectionSettings, requestData.DisableDirectStreaming);
			}
			else
				postData.Write(stream, requestData.ConnectionSettings, requestData.DisableDirectStreaming);

			// the written bytes are uncompressed, so can only be used when http compression isn't used
			if (requestData.DisableDirectStreaming && !requestData.HttpCompression)
			{
				message.Content = new ByteArrayContent(postData.WrittenBytes);
				stream.Dispose();
			}
			else
			{
				stream.Position = 0;
				message.Content = new StreamContent(stream);
			}

			if (requestData.HttpCompression)
				message.Content.Headers.ContentEncoding.Add("gzip");

			message.Content.Headers.TryAddWithoutValidation("Content-Type", requestData.ContentType);
		}
	}

	private static async Task SetContentAsync(HttpRequestMessage message, RequestData requestData, PostData postData, CancellationToken cancellationToken)
	{
		if (requestData.TransferEncodingChunked)
			message.Content = new RequestDataContent(requestData, cancellationToken);
		else
		{
			var stream = requestData.MemoryStreamFactory.Create();
			if (requestData.HttpCompression)
			{
				using var zipStream = new GZipStream(stream, CompressionMode.Compress, true);
				await postData.WriteAsync(zipStream, requestData.ConnectionSettings, requestData.DisableDirectStreaming, cancellationToken).ConfigureAwait(false);
			}
			else
				await postData.WriteAsync(stream, requestData.ConnectionSettings, requestData.DisableDirectStreaming, cancellationToken).ConfigureAwait(false);

			// the written bytes are uncompressed, so can only be used when http compression isn't used
			if (requestData.DisableDirectStreaming && !requestData.HttpCompression)
			{
				message.Content = new ByteArrayContent(postData.WrittenBytes);
#if DOTNETCORE_2_1_OR_HIGHER
					await stream.DisposeAsync().ConfigureAwait(false);
#else
				stream.Dispose();
#endif
			}
			else
			{
				stream.Position = 0;
				message.Content = new StreamContent(stream);
			}

			if (requestData.HttpCompression)
				message.Content.Headers.ContentEncoding.Add("gzip");

			message.Content.Headers.TryAddWithoutValidation("Content-Type", requestData.ContentType);
		}
	}

	private static System.Net.Http.HttpMethod ConvertHttpMethod(HttpMethod httpMethod)
	{
		switch (httpMethod)
		{
			case HttpMethod.GET: return System.Net.Http.HttpMethod.Get;
			case HttpMethod.POST: return System.Net.Http.HttpMethod.Post;
			case HttpMethod.PUT: return System.Net.Http.HttpMethod.Put;
			case HttpMethod.DELETE: return System.Net.Http.HttpMethod.Delete;
			case HttpMethod.HEAD: return System.Net.Http.HttpMethod.Head;
			default:
				throw new ArgumentException("Invalid value for HttpMethod", nameof(httpMethod));
		}
	}

	internal static int GetClientKey(RequestData requestData)
	{
		unchecked
		{
			var hashCode = requestData.RequestTimeout.GetHashCode();
			hashCode = (hashCode * 397) ^ requestData.HttpCompression.GetHashCode();
			hashCode = (hashCode * 397) ^ (requestData.ProxyAddress?.GetHashCode() ?? 0);
			hashCode = (hashCode * 397) ^ (requestData.ProxyUsername?.GetHashCode() ?? 0);
			hashCode = (hashCode * 397) ^ (requestData.ProxyPassword?.GetHashCode() ?? 0);
			hashCode = (hashCode * 397) ^ requestData.DisableAutomaticProxyDetection.GetHashCode();
			return hashCode;
		}
	}

	/// <summary> Allows subclasses to dispose of managed resources </summary>
	public virtual void DisposeManagedResources() {}
	/// <inheritdoc />
	public void Dispose()
	{
		HttpClientFactory.Dispose();
		DisposeManagedResources();
	}
}
#endif
