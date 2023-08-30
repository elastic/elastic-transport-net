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

/// <summary> The default TransportClient implementation. Uses <see cref="HttpClient" />.</summary>
public class HttpRequestInvoker : IRequestInvoker
{
	private static readonly string MissingConnectionLimitMethodError =
		$"Your target platform does not support {nameof(TransportConfiguration.ConnectionLimit)}"
		+ $" please set {nameof(TransportConfiguration.ConnectionLimit)} to -1 on your connection configuration/settings."
		+ $" this will cause the {nameof(HttpClientHandler.MaxConnectionsPerServer)} not to be set on {nameof(HttpClientHandler)}";

	private string _expectedCertificateFingerprint;

	/// <inheritdoc cref="HttpRequestInvoker" />
	public HttpRequestInvoker() => HttpClientFactory = new RequestDataHttpClientFactory(r => CreateHttpClientHandler(r));

	/// <summary>
	/// Allows users to inject their own HttpMessageHandler, and optionally call our default implementation
	/// </summary>
	public HttpRequestInvoker(Func<Func<RequestData, HttpMessageHandler>, RequestData, HttpMessageHandler> createHttpHandler) =>
		HttpClientFactory = new RequestDataHttpClientFactory(r => CreateHttpClientHandler(r));

	/// <inheritdoc cref="RequestDataHttpClientFactory.InUseHandlers" />
	public int InUseHandlers => HttpClientFactory.InUseHandlers;

	/// <inheritdoc cref="RequestDataHttpClientFactory.RemovedHandlers" />
	public int RemovedHandlers => HttpClientFactory.RemovedHandlers;

	private static DiagnosticSource DiagnosticSource { get; } = new DiagnosticListener(DiagnosticSources.HttpConnection.SourceName);

	private RequestDataHttpClientFactory HttpClientFactory { get; }

	/// <inheritdoc cref="HttpRequestInvoker.Request{TResponse}" />
	public override TResponse Request<TResponse>(RequestData requestData) =>
		RequestCoreAsync<TResponse>(false, requestData).EnsureCompleted();

	/// <inheritdoc cref="HttpRequestInvoker.RequestAsync{TResponse}" />
	public override Task<TResponse> RequestAsync<TResponse>(RequestData requestData, CancellationToken cancellationToken) =>
		RequestCoreAsync<TResponse>(true, requestData, cancellationToken).AsTask();

	private async ValueTask<TResponse> RequestCoreAsync<TResponse>(bool isAsync, RequestData requestData, CancellationToken cancellationToken = default)
		where TResponse : TransportResponse, new()
	{
		var client = GetClient(requestData);
		HttpResponseMessage responseMessage;
		int? statusCode = null;
		Stream responseStream = null;
		Exception ex = null;
		string mimeType = null;
		long contentLength = -1;
		IDisposable receive = DiagnosticSources.SingletonDisposable;
		ReadOnlyDictionary<TcpState, int> tcpStats = null;
		ReadOnlyDictionary<string, ThreadPoolStatistics> threadPoolStats = null;
		Dictionary<string, IEnumerable<string>> responseHeaders = null;
		requestData.IsAsync = isAsync;

		var beforeTicks = Stopwatch.GetTimestamp();

		try
		{
			var requestMessage = CreateHttpRequestMessage(requestData);

			if (requestData.PostData is not null)
			{
				if (isAsync)
					await SetContentAsync(requestMessage, requestData, cancellationToken).ConfigureAwait(false);
				else
					SetContent(requestMessage, requestData);
			}

			using (requestMessage?.Content ?? (IDisposable)Stream.Null)
			{
				if (requestData.TcpStats)
					tcpStats = TcpStats.GetStates();

				if (requestData.ThreadPoolStats)
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

				statusCode = (int)responseMessage.StatusCode;
			}

			requestData.MadeItToResponse = true;
			mimeType = responseMessage.Content.Headers.ContentType?.ToString();
			responseHeaders = ParseHeaders(requestData, responseMessage, responseHeaders);

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

			// We often won't have the content length as responses are GZip compressed and the HttpContent ditches this when AutomaticDecompression is enabled.
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
		using (receive)
		using (responseStream ??= Stream.Null)
		{
			TResponse response;

			if (isAsync)
				response = await requestData.ConnectionSettings.ProductRegistration.ResponseBuilder.ToResponseAsync<TResponse>
					(requestData, ex, statusCode, responseHeaders, responseStream, mimeType, contentLength, threadPoolStats, tcpStats, cancellationToken)
						.ConfigureAwait(false);
			else
				response = requestData.ConnectionSettings.ProductRegistration.ResponseBuilder.ToResponse<TResponse>
						(requestData, ex, statusCode, responseHeaders, responseStream, mimeType, contentLength, threadPoolStats, tcpStats);

			if (OpenTelemetry.CurrentSpanIsElasticTransportOwnedAndHasListeners && (Activity.Current?.IsAllDataRequested ?? false))
			{
				var attributes = requestData.ConnectionSettings.ProductRegistration.ParseOpenTelemetryAttributesFromApiCallDetails(response.ApiCallDetails);

				if (attributes is not null)
				{
					foreach (var attribute in attributes)
					{
						Activity.Current?.SetTag(attribute.Key, attribute.Value);
					}
				}
			}

			return response;
		}
	}

	private static Dictionary<string, IEnumerable<string>> ParseHeaders(RequestData requestData,
		HttpResponseMessage responseMessage, Dictionary<string, IEnumerable<string>> responseHeaders)
	{
		var defaultHeadersForProduct = requestData.ConnectionSettings.ProductRegistration.DefaultHeadersToParse();
		foreach (var headerToParse in defaultHeadersForProduct)
		{
			if (responseMessage.Headers.TryGetValues(headerToParse, out var values))
			{
				responseHeaders ??= new Dictionary<string, IEnumerable<string>>();
				responseHeaders.Add(headerToParse, values);
			}
		}

		if (requestData.ParseAllHeaders)
		{
			foreach (var header in responseMessage.Headers)
			{
				responseHeaders ??= new Dictionary<string, IEnumerable<string>>();
				responseHeaders.Add(header.Key, header.Value);
			}
		}
		else if (requestData.ResponseHeadersToParse.Count > 0)
		{
			foreach (var headerToParse in requestData.ResponseHeadersToParse)
			{
				if (responseMessage.Headers.TryGetValues(headerToParse, out var values))
				{
					responseHeaders ??= new Dictionary<string, IEnumerable<string>>();
					responseHeaders.Add(headerToParse, values);
				}
			}
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
	/// <param name="requestData">An instance of <see cref="RequestData" /> describing where and how to call out to</param>
	/// <exception cref="Exception">
	/// Can throw if <see cref="ITransportConfiguration.ConnectionLimit" /> is set but the platform does
	/// not allow this to be set on <see cref="HttpClientHandler.MaxConnectionsPerServer" />
	/// </exception>
	internal HttpRequestMessage CreateHttpRequestMessage(RequestData requestData)
	{
		var request = CreateRequestMessage(requestData);
		SetAuthenticationIfNeeded(request, requestData);
		return request;
	}

	/// <summary> Isolated hook for subclasses to set authentication on <paramref name="requestMessage" /> </summary>
	/// <param name="requestMessage">The instance of <see cref="HttpRequestMessage" /> that needs authentication details</param>
	/// <param name="requestData">An object describing where and how we want to call out to</param>
	internal void SetAuthenticationIfNeeded(HttpRequestMessage requestMessage, RequestData requestData)
	{
		//If user manually specifies an Authorization Header give it preference
		if (requestData.Headers.HasKeys() && requestData.Headers.AllKeys.Contains("Authorization"))
		{
			var header = AuthenticationHeaderValue.Parse(requestData.Headers["Authorization"]);
			requestMessage.Headers.Authorization = header;
			return;
		}

		SetConfiguredAuthenticationHeaderIfNeeded(requestMessage, requestData);
	}

	private static void SetConfiguredAuthenticationHeaderIfNeeded(HttpRequestMessage requestMessage, RequestData requestData)
	{
		// Basic auth credentials take the following precedence (highest -> lowest):
		// 1 - Specified with the URI (highest precedence)
		// 2 - Specified on the request
		// 3 - Specified at the global TransportClientSettings level (lowest precedence)

		string parameters = null;
		string scheme = null;
		if (!requestData.Uri.UserInfo.IsNullOrEmpty())
		{
			parameters = BasicAuthentication.GetBase64String(Uri.UnescapeDataString(requestData.Uri.UserInfo));
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

	private static HttpRequestMessage CreateRequestMessage(RequestData requestData)
	{
		var method = ConvertHttpMethod(requestData.Method);
		var requestMessage = new HttpRequestMessage(method, requestData.Uri);

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
			var value = requestData.MetaHeaderProvider.ProduceHeaderValue(requestData);

			if (!string.IsNullOrEmpty(value))
				requestMessage.Headers.TryAddWithoutValidation(requestData.MetaHeaderProvider.HeaderName, value);
		}

		return requestMessage;
	}

	private static void SetContent(HttpRequestMessage message, RequestData requestData)
	{
		if (requestData.TransferEncodingChunked)
			message.Content = new RequestDataContent(requestData);
		else
		{
			var stream = requestData.MemoryStreamFactory.Create();
			if (requestData.HttpCompression)
			{
				using var zipStream = new GZipStream(stream, CompressionMode.Compress, true);
				requestData.PostData.Write(zipStream, requestData.ConnectionSettings);
			}
			else
				requestData.PostData.Write(stream, requestData.ConnectionSettings);

			// the written bytes are uncompressed, so can only be used when http compression isn't used
			if (requestData.PostData.DisableDirectStreaming.GetValueOrDefault(false) && !requestData.HttpCompression)
			{
				message.Content = new ByteArrayContent(requestData.PostData.WrittenBytes);
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

	private static async Task SetContentAsync(HttpRequestMessage message, RequestData requestData, CancellationToken cancellationToken)
	{
		if (requestData.TransferEncodingChunked)
			message.Content = new RequestDataContent(requestData, cancellationToken);
		else
		{
			var stream = requestData.MemoryStreamFactory.Create();
			if (requestData.HttpCompression)
			{
				using var zipStream = new GZipStream(stream, CompressionMode.Compress, true);
				await requestData.PostData.WriteAsync(zipStream, requestData.ConnectionSettings, cancellationToken).ConfigureAwait(false);
			}
			else
				await requestData.PostData.WriteAsync(stream, requestData.ConnectionSettings, cancellationToken).ConfigureAwait(false);

			// the written bytes are uncompressed, so can only be used when http compression isn't used
			if (requestData.PostData.DisableDirectStreaming.GetValueOrDefault(false) && !requestData.HttpCompression)
			{
				message.Content = new ByteArrayContent(requestData.PostData.WrittenBytes);
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
