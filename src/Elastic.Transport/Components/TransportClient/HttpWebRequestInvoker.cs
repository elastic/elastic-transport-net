// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Security;
using System.Threading;
using System.Threading.Tasks;
using Elastic.Transport.Diagnostics;
using Elastic.Transport.Extensions;

namespace Elastic.Transport;

/// <summary>
/// This provides an <see cref="IRequestInvoker"/> implementation that targets <see cref="HttpWebRequest"/>.
/// <para>
/// On .NET full framework <see cref="HttpRequestInvoker"/> is an alias to this.
/// </para>
/// <para/>
/// <para>Do NOT use this class directly on .NET Core. <see cref="HttpWebRequest"/> is monkey patched
/// over HttpClient and does not reuse its instances of HttpClient
/// </para>
/// </summary>
#if !NETFRAMEWORK
[Obsolete("CoreFX HttpWebRequest uses HttpClient under the covers but does not reuse HttpClient instances, do NOT use on .NET core only used as the default on Full Framework")]
#endif
public class HttpWebRequestInvoker : IRequestInvoker
{
	private string _expectedCertificateFingerprint;

	static HttpWebRequestInvoker()
	{
		//Not available under mono
		if (!IsMono) HttpWebRequest.DefaultMaximumErrorResponseLength = -1;
	}

	/// <inheritdoc cref="HttpWebRequestInvoker"/>>
	public HttpWebRequestInvoker() { }

	internal static bool IsMono { get; } = Type.GetType("Mono.Runtime") != null;

	void IDisposable.Dispose() {}

	/// <inheritdoc cref="IRequestInvoker.Request{TResponse}"/>>
	public TResponse Request<TResponse>(Endpoint endpoint, RequestData requestData, PostData? postData)
		where TResponse : TransportResponse, new() =>
		RequestCoreAsync<TResponse>(false, endpoint, requestData, postData).EnsureCompleted();

	/// <inheritdoc cref="IRequestInvoker.RequestAsync{TResponse}"/>>
	public Task<TResponse> RequestAsync<TResponse>(Endpoint endpoint, RequestData requestData, PostData? postData, CancellationToken cancellationToken = default)
		where TResponse : TransportResponse, new() =>
		RequestCoreAsync<TResponse>(true, endpoint, requestData, postData, cancellationToken).AsTask();

	private async ValueTask<TResponse> RequestCoreAsync<TResponse>(bool isAsync, Endpoint endpoint, RequestData requestData, PostData? postData, CancellationToken cancellationToken = default)
		where TResponse : TransportResponse, new()
	{
		Action unregisterWaitHandle = null;
		int? statusCode = null;
		Stream responseStream = null;
		Exception ex = null;
		string mimeType = null;
		long contentLength = -1;
		IDisposable receivedResponse = DiagnosticSources.SingletonDisposable;
		ReadOnlyDictionary<TcpState, int> tcpStats = null;
		ReadOnlyDictionary<string, ThreadPoolStatistics> threadPoolStats = null;
		Dictionary<string, IEnumerable<string>> responseHeaders = null;
		requestData.IsAsync = true;

		var beforeTicks = Stopwatch.GetTimestamp();

		try
		{
			var data = postData;
			var request = CreateHttpWebRequest(endpoint, requestData, postData);
			using (cancellationToken.Register(() => request.Abort()))
			{
				if (data is not null)
				{
					if (isAsync)
					{
						var apmGetRequestStreamTask =
						Task.Factory.FromAsync(request.BeginGetRequestStream, request.EndGetRequestStream, null);
						unregisterWaitHandle = RegisterApmTaskTimeout(apmGetRequestStreamTask, request, requestData);

						using (var stream = await apmGetRequestStreamTask.ConfigureAwait(false))
						{
							if (requestData.HttpCompression)
							{
								using var zipStream = new GZipStream(stream, CompressionMode.Compress);
								await data.WriteAsync(zipStream, requestData.ConnectionSettings, requestData.DisableDirectStreaming, cancellationToken).ConfigureAwait(false);
							}
							else
								await data.WriteAsync(stream, requestData.ConnectionSettings, requestData.DisableDirectStreaming, cancellationToken).ConfigureAwait(false);
						}
						unregisterWaitHandle?.Invoke();
					}
					else
					{
						using var stream = request.GetRequestStream();

						if (requestData.HttpCompression)
						{
							using var zipStream = new GZipStream(stream, CompressionMode.Compress);
							data.Write(zipStream, requestData.ConnectionSettings, requestData.DisableDirectStreaming);
						}
						else
							data.Write(stream, requestData.ConnectionSettings, requestData.DisableDirectStreaming);
					}
				}

				var prepareRequestMs = (Stopwatch.GetTimestamp() - beforeTicks) / (Stopwatch.Frequency / 1000);

				if (prepareRequestMs > OpenTelemetry.MinimumMillisecondsToEmitTimingSpanAttribute && OpenTelemetry.CurrentSpanIsElasticTransportOwnedHasListenersAndAllDataRequested)
					Activity.Current?.SetTag(OpenTelemetryAttributes.ElasticTransportPrepareRequestMs, prepareRequestMs);

				//http://msdn.microsoft.com/en-us/library/system.net.httpwebresponse.getresponsestream.aspx
				//Either the stream or the response object needs to be closed but not both although it won't
				//throw any errors if both are closed atleast one of them has to be Closed.
				//Since we expose the stream we let closing the stream determining when to close the connection

				if (requestData.TcpStats)
					tcpStats = TcpStats.GetStates();

				if (requestData.ThreadPoolStats)
					threadPoolStats = ThreadPoolStats.GetStats();

				HttpWebResponse httpWebResponse;

				if (isAsync)
				{
					var apmGetResponseTask = Task.Factory.FromAsync(request.BeginGetResponse, r => request.EndGetResponse(r), null);
					unregisterWaitHandle = RegisterApmTaskTimeout(apmGetResponseTask, request, requestData);
					httpWebResponse = (HttpWebResponse)await apmGetResponseTask.ConfigureAwait(false);
				}
				else
				{
					httpWebResponse = (HttpWebResponse)request.GetResponse();
				}

				receivedResponse = httpWebResponse;

				HandleResponse(httpWebResponse, out statusCode, out responseStream, out mimeType);
				responseHeaders = ParseHeaders(requestData, httpWebResponse, responseHeaders);
				contentLength = httpWebResponse.ContentLength;
			}
		}
		catch (WebException e)
		{
			ex = e;
			if (e.Response is HttpWebResponse httpWebResponse)
				HandleResponse(httpWebResponse, out statusCode, out responseStream, out mimeType);
		}
		finally
		{
			unregisterWaitHandle?.Invoke();
		}

		try
		{
			TResponse response;

		if (isAsync)
			response = await requestData.ConnectionSettings.ProductRegistration.ResponseBuilder.ToResponseAsync<TResponse>
				(endpoint, requestData, postData, ex, statusCode, responseHeaders, responseStream, mimeType, contentLength, threadPoolStats, tcpStats, cancellationToken)
					.ConfigureAwait(false);
		else
			response = requestData.ConnectionSettings.ProductRegistration.ResponseBuilder.ToResponse<TResponse>
					(endpoint, requestData, postData, ex, statusCode, responseHeaders, responseStream, mimeType, contentLength, threadPoolStats, tcpStats);

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

			if (OpenTelemetry.CurrentSpanIsElasticTransportOwnedAndHasListeners && (Activity.Current?.IsAllDataRequested ?? false))
			{
				var attributes = requestData.ConnectionSettings.ProductRegistration.ParseOpenTelemetryAttributesFromApiCallDetails(response.ApiCallDetails);
				foreach (var attribute in attributes)
				{
					Activity.Current?.SetTag(attribute.Key, attribute.Value);
				}
			}

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

	private static Dictionary<string, IEnumerable<string>> ParseHeaders(RequestData requestData, HttpWebResponse responseMessage, Dictionary<string, IEnumerable<string>> responseHeaders)
	{
		if (!responseMessage.SupportsHeaders && !responseMessage.Headers.HasKeys()) return null;

		var defaultHeadersForProduct = requestData.ConnectionSettings.ProductRegistration.DefaultHeadersToParse();
		foreach (var headerToParse in defaultHeadersForProduct)
		{
			if (responseMessage.Headers.AllKeys.Contains(headerToParse, StringComparer.OrdinalIgnoreCase))
			{
				responseHeaders ??= new Dictionary<string, IEnumerable<string>>();
				responseHeaders.Add(headerToParse, responseMessage.Headers.GetValues(headerToParse));
			}
		}

		if (requestData.ParseAllHeaders)
		{
			foreach (var key in responseMessage.Headers.AllKeys)
			{
				responseHeaders ??= new Dictionary<string, IEnumerable<string>>();
				responseHeaders.Add(key, responseMessage.Headers.GetValues(key));
			}
		}
		else if (requestData.ResponseHeadersToParse is { Count: > 0 })
		{
			foreach (var headerToParse in requestData.ResponseHeadersToParse)
			{
				if (responseMessage.Headers.AllKeys.Contains(headerToParse, StringComparer.OrdinalIgnoreCase))
				{
					responseHeaders ??= new Dictionary<string, IEnumerable<string>>();
					responseHeaders.Add(headerToParse, responseMessage.Headers.GetValues(headerToParse));
				}
			}
		}

		return responseHeaders;
	}

	/// <summary>
	/// Allows subclasses to modify the <see cref="HttpWebRequest"/> instance that is going to be used for the API call
	/// </summary>
	/// <param name="endpoint">An instance of <see cref="Endpoint"/> describing where to call out to</param>
	/// <param name="requestData">An instance of <see cref="RequestData"/> describing how to call out to</param>
	/// <param name="postData">Optional data to send over the wire</param>
	protected virtual HttpWebRequest CreateHttpWebRequest(Endpoint endpoint, RequestData requestData, PostData? postData)
	{
		var request = CreateWebRequest(endpoint, requestData, postData);
		SetAuthenticationIfNeeded(endpoint, requestData, request);
		SetProxyIfNeeded(request, requestData);
		SetServerCertificateValidationCallBackIfNeeded(request, requestData);
		SetClientCertificates(request, requestData);
		AlterServicePoint(request.ServicePoint, requestData);
		return request;
	}

	/// <summary> Hook for subclasses to set additional client certificates on <paramref name="request"/> </summary>
	protected virtual void SetClientCertificates(HttpWebRequest request, RequestData requestData)
	{
		if (requestData.ClientCertificates != null)
			request.ClientCertificates.AddRange(requestData.ClientCertificates);
	}

	private string ComparableFingerprint(string fingerprint)
	{
		var finalFingerprint = fingerprint;
		if (fingerprint.Contains(':'))
		{
			finalFingerprint = fingerprint.Replace(":", string.Empty);
		}
		else if (fingerprint.Contains('-'))
		{
			finalFingerprint = fingerprint.Replace("-", string.Empty);
		}
		return finalFingerprint;
	}

	/// <summary> Hook for subclasses override the certificate validation on <paramref name="request"/> </summary>
	protected virtual void SetServerCertificateValidationCallBackIfNeeded(HttpWebRequest request, RequestData requestData)
	{
		var callback = requestData?.ConnectionSettings?.ServerCertificateValidationCallback;
#if !__MonoCS__
		//Only assign if one is defined on connection settings and a subclass has not already set one
		if (callback != null && request.ServerCertificateValidationCallback == null)
		{
			request.ServerCertificateValidationCallback = new RemoteCertificateValidationCallback(callback);
		}
		else if (!string.IsNullOrEmpty(requestData.ConnectionSettings.CertificateFingerprint))
		{
			request.ServerCertificateValidationCallback = new RemoteCertificateValidationCallback((request, certificate, chain, policyErrors) =>
			{
				if (certificate is null && chain is null) return false;

				// The "cleaned", expected fingerprint is cached to avoid repeated cost of converting it to a comparable form.
				_expectedCertificateFingerprint  ??= CertificateHelpers.ComparableFingerprint(requestData.ConnectionSettings.CertificateFingerprint);

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
			});
		}
#else
		if (callback != null)
			throw new Exception("Mono misses ServerCertificateValidationCallback on HttpWebRequest");
#endif
	}

	private static HttpWebRequest CreateWebRequest(Endpoint endpoint, RequestData requestData, PostData? postData)
	{
		var request = (HttpWebRequest)WebRequest.Create(endpoint.Uri);

		request.Accept = requestData.Accept;
		request.ContentType = requestData.ContentType;
#if NETFRAMEWORK
		// on netstandard/netcoreapp2.0 this throws argument exception
		request.MaximumResponseHeadersLength = -1;
#endif
		request.Pipelined = requestData.Pipelined;

		if (requestData.TransferEncodingChunked)
			request.SendChunked = true;

		if (requestData.HttpCompression)
		{
			request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
			request.Headers.Add("Accept-Encoding", "gzip,deflate");
			request.Headers.Add("Content-Encoding", "gzip");
		}

		var userAgent = requestData.UserAgent?.ToString();
		if (!string.IsNullOrWhiteSpace(userAgent))
			request.UserAgent = userAgent;

		if (!string.IsNullOrWhiteSpace(requestData.RunAs))
			request.Headers.Add(RequestData.RunAsSecurityHeader, requestData.RunAs);

		if (requestData.Headers != null && requestData.Headers.HasKeys())
			request.Headers.Add(requestData.Headers);

		if (requestData.MetaHeaderProvider is not null)
		{
			foreach (var producer in requestData.MetaHeaderProvider.Producers)
			{
				var value = producer.ProduceHeaderValue(requestData);

				if (!string.IsNullOrEmpty(value))
					request.Headers.Add(producer.HeaderName, value);
			}
		}

		var timeout = (int)requestData.RequestTimeout.TotalMilliseconds;
		request.Timeout = timeout;
		request.ReadWriteTimeout = timeout;

		//WebRequest won't send Content-Length: 0 for empty bodies
		//which goes against RFC's and might break i.e IIS when used as a proxy.
		//see: https://github.com/elastic/elasticsearch-net/issues/562
		var m = endpoint.Method.GetStringValue();
		request.Method = m;
		if (m != "HEAD" && m != "GET" && postData == null)
			request.ContentLength = 0;

		return request;
	}

	/// <summary> Hook for subclasses override <see cref="ServicePoint"/> behavior</summary>
	protected virtual void AlterServicePoint(ServicePoint requestServicePoint, RequestData requestData)
	{
		requestServicePoint.UseNagleAlgorithm = false;
		requestServicePoint.Expect100Continue = false;
		requestServicePoint.ConnectionLeaseTimeout = (int)requestData.DnsRefreshTimeout.TotalMilliseconds;
		if (requestData.ConnectionSettings.ConnectionLimit > 0)
			requestServicePoint.ConnectionLimit = requestData.ConnectionSettings.ConnectionLimit;
		//looking at http://referencesource.microsoft.com/#System/net/System/Net/ServicePoint.cs
		//this method only sets internal values and wont actually cause timers and such to be reset
		//So it should be idempotent if called with the same parameters
		requestServicePoint.SetTcpKeepAlive(true, requestData.KeepAliveTime, requestData.KeepAliveInterval);
	}

	/// <summary> Hook for subclasses to set proxy on <paramref name="request"/> </summary>
	protected virtual void SetProxyIfNeeded(HttpWebRequest request, RequestData requestData)
	{
		if (!string.IsNullOrWhiteSpace(requestData.ProxyAddress))
		{
			var uri = new Uri(requestData.ProxyAddress);
			var proxy = new WebProxy(uri);
			var credentials = new NetworkCredential(requestData.ProxyUsername, requestData.ProxyPassword);
			proxy.Credentials = credentials;
			request.Proxy = proxy;
		}

		if (requestData.DisableAutomaticProxyDetection)
			request.Proxy = null!;
	}

	/// <summary> Hook for subclasses to set authentication on <paramref name="request"/></summary>
	protected virtual void SetAuthenticationIfNeeded(Endpoint endpoint, RequestData requestData, HttpWebRequest request)
	{
		//If user manually specifies an Authorization Header give it preference
		if (requestData.Headers.HasKeys() && requestData.Headers.AllKeys.Contains("Authorization"))
		{
			var header = requestData.Headers["Authorization"];
			request.Headers["Authorization"] = header;
			return;
		}
		SetBasicAuthenticationIfNeeded(endpoint, requestData, request);
	}

	private static void SetBasicAuthenticationIfNeeded(Endpoint endpoint, RequestData requestData, HttpWebRequest request)
	{
		// Basic auth credentials take the following precedence (highest -> lowest):
		// 1 - Specified on the request (highest precedence)
		// 2 - Specified at the global TransportClientSettings level
		// 3 - Specified with the URI (lowest precedence)


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

		request.Headers["Authorization"] = $"{scheme} {parameters}";
	}

	/// <summary>
	/// Registers an APM async task cancellation on the threadpool
	/// </summary>
	/// <returns>An unregister action that can be used to remove the waithandle prematurely</returns>
	private static Action RegisterApmTaskTimeout(IAsyncResult result, WebRequest request, RequestData requestData)
	{
		var waitHandle = result.AsyncWaitHandle;
		var registeredWaitHandle =
			ThreadPool.RegisterWaitForSingleObject(waitHandle, TimeoutCallback, request, requestData.RequestTimeout, true);
		return () => registeredWaitHandle.Unregister(waitHandle);
	}

	private static void TimeoutCallback(object state, bool timedOut)
	{
		if (!timedOut) return;

		(state as WebRequest)?.Abort();
	}

	private static void HandleResponse(HttpWebResponse response, out int? statusCode, out Stream responseStream, out string mimeType)
	{
		statusCode = (int)response.StatusCode;
		responseStream = response.GetResponseStream();
		mimeType = response.ContentType;
		// https://github.com/elastic/elasticsearch-net/issues/2311
		// if stream is null call dispose on response instead.
		if (responseStream == null || responseStream == Stream.Null) response.Dispose();
	}

}
