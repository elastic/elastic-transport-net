// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
#if DOTNETCORE
using System.Net.Http;
#endif
using System.Net.Security;
using System.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using Elastic.Transport.Extensions;
using Elastic.Transport.Products;

namespace Elastic.Transport
{
	/// <summary>
	/// Allows you to control how <see cref="ITransport{TConnectionSettings}"/> behaves and where/how it connects to Elastic Stack products
	/// </summary>
	public class TransportConfiguration : TransportConfigurationBase<TransportConfiguration>
	{
		/// <summary>
		/// Detects whether we are running on .NET Core with CurlHandler.
		/// If this is true, we will set a very restrictive <see cref="DefaultConnectionLimit"/>
		/// As the old curl based handler is known to bleed TCP connections:
		/// <para>https://github.com/dotnet/runtime/issues/22366</para>
		/// </summary>
        private static bool UsingCurlHandler
		{
			get
			{
#if !DOTNETCORE
				return false;
#else
				var curlHandlerExists = typeof(HttpClientHandler).Assembly.GetType("System.Net.Http.CurlHandler") != null;
				if (!curlHandlerExists) return false;

				var socketsHandlerExists = typeof(HttpClientHandler).Assembly.GetType("System.Net.Http.SocketsHttpHandler") != null;
				// running on a .NET core version with CurlHandler, before the existence of SocketsHttpHandler.
				// Must be using CurlHandler.
				if (!socketsHandlerExists) return true;

				if (AppContext.TryGetSwitch("System.Net.Http.UseSocketsHttpHandler", out var isEnabled))
					return !isEnabled;

				var environmentVariable =
					Environment.GetEnvironmentVariable("DOTNET_SYSTEM_NET_HTTP_USESOCKETSHTTPHANDLER");

				// SocketsHandler exists and no environment variable exists to disable it.
				// Must be using SocketsHandler and not CurlHandler
				if (environmentVariable == null) return false;

				return environmentVariable.Equals("false", StringComparison.OrdinalIgnoreCase) ||
					environmentVariable.Equals("0");
#endif
			}
		}

		//public static IMemoryStreamFactory Default { get; } = RecyclableMemoryStreamFactory.Default;
		// ReSharper disable once RedundantNameQualifier
		/// <summary>
		/// The default memory stream factory if none is configured on <see cref="ITransportConfigurationValues.MemoryStreamFactory"/>
		/// </summary>
		public static IMemoryStreamFactory DefaultMemoryStreamFactory { get; } = Elastic.Transport.MemoryStreamFactory.Default;

		/// <summary>
		/// The default ping timeout. Defaults to 2 seconds
		/// </summary>
		public static readonly TimeSpan DefaultPingTimeout = TimeSpan.FromSeconds(2);

		/// <summary>
		/// The default ping timeout when the connection is over HTTPS. Defaults to
		/// 5 seconds
		/// </summary>
		public static readonly TimeSpan DefaultPingTimeoutOnSsl = TimeSpan.FromSeconds(5);

		/// <summary>
		/// The default timeout before the client aborts a request to Elasticsearch.
		/// Defaults to 1 minute
		/// </summary>
		public static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(1);

		/// <summary>
		/// The default timeout before a TCP connection is forcefully recycled so that DNS updates come through
		/// Defaults to 5 minutes.
		/// </summary>
		public static readonly TimeSpan DefaultDnsRefreshTimeout = TimeSpan.FromMinutes(5);

#pragma warning disable 1587
#pragma warning disable 1570
		/// <summary>
		/// The default concurrent connection limit for outgoing http requests. Defaults to <c>80</c>
#if DOTNETCORE
		/// <para>Except for <see cref="HttpClientHandler"/> implementations based on curl, which defaults to <see cref="Environment.ProcessorCount"/></para>
#endif
		/// </summary>
#pragma warning restore 1570
#pragma warning restore 1587
		public static readonly int DefaultConnectionLimit = UsingCurlHandler ? Environment.ProcessorCount : 80;

		/// <summary>
		/// Creates a new instance of <see cref="TransportConfiguration"/>
		/// </summary>
		/// <param name="uri">The root of the Elastic stack product node we want to connect to. Defaults to http://localhost:9200</param>
		/// <param name="productRegistration"><inheritdoc cref="IProductRegistration"/></param>
		[SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope")]
		public TransportConfiguration(Uri uri = null, IProductRegistration productRegistration = null)
			: this(new SingleNodeConnectionPool(uri ?? new Uri("http://localhost:9200")), productRegistration: productRegistration) { }

		/// <summary>
		/// Sets up the client to communicate to Elastic Cloud using <paramref name="cloudId"/>,
		/// <para><see cref="CloudConnectionPool"/> documentation for more information on how to obtain your Cloud Id</para>
		/// </summary>
		public TransportConfiguration(string cloudId, BasicAuthenticationCredentials credentials, IProductRegistration productRegistration = null)
			: this(new CloudConnectionPool(cloudId, credentials), productRegistration: productRegistration) { }

		/// <summary>
		/// Sets up the client to communicate to Elastic Cloud using <paramref name="cloudId"/>,
		/// <para><see cref="CloudConnectionPool"/> documentation for more information on how to obtain your Cloud Id</para>
		/// </summary>
		public TransportConfiguration(string cloudId, ApiKeyAuthenticationCredentials credentials, IProductRegistration productRegistration = null)
			: this(new CloudConnectionPool(cloudId, credentials), productRegistration: productRegistration) { }

		/// <summary> <inheritdoc cref="TransportConfiguration"/></summary>
		/// <param name="connectionPool"><inheritdoc cref="IConnectionPool"/></param>
		/// <param name="connection"><inheritdoc cref="IConnection"/></param>
		/// <param name="serializer"><inheritdoc cref="ITransportSerializer"/></param>
		/// <param name="productRegistration"><inheritdoc cref="IProductRegistration"/></param>
		public TransportConfiguration(
			IConnectionPool connectionPool,
			IConnection connection = null,
			ITransportSerializer serializer = null,
			IProductRegistration productRegistration = null)
			: base(connectionPool, connection, serializer, productRegistration) { }

	}

	/// <inheritdoc cref="TransportConfiguration"/>>
	[Browsable(false)]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public abstract class TransportConfigurationBase<T> : ITransportConfigurationValues
		where T : TransportConfigurationBase<T>
	{
		private readonly IConnection _connection;
		private readonly IConnectionPool _connectionPool;
		private readonly IProductRegistration _productRegistration;
		private readonly NameValueCollection _headers = new NameValueCollection();
		private readonly NameValueCollection _queryString = new NameValueCollection();
		private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
		private readonly UrlFormatter _urlFormatter;

		private BasicAuthenticationCredentials _basicAuthCredentials;
		private ApiKeyAuthenticationCredentials _apiKeyAuthCredentials;
		private X509CertificateCollection _clientCertificates;
		private Action<IApiCallDetails> _completedRequestHandler = DefaultCompletedRequestHandler;
		private int _connectionLimit;
		private TimeSpan? _deadTimeout;
		private bool _disableAutomaticProxyDetection;
		private bool _disableDirectStreaming;
		private bool _disablePings;
		private bool _enableHttpCompression;
		private bool _enableHttpPipelining = true;
		private TimeSpan? _keepAliveInterval;
		private TimeSpan? _keepAliveTime;
		private TimeSpan? _maxDeadTimeout;
		private int? _maxRetries;
		private TimeSpan? _maxRetryTimeout;
		private Func<Node, bool> _nodePredicate;
		private Action<RequestData> _onRequestDataCreated = DefaultRequestDataCreated;
		private TimeSpan? _pingTimeout;
		private string _proxyAddress;
		private SecureString _proxyPassword;
		private string _proxyUsername;
		private TimeSpan _requestTimeout;
		private TimeSpan _dnsRefreshTimeout;
		private Func<object, X509Certificate, X509Chain, SslPolicyErrors, bool> _serverCertificateValidationCallback;
		private IReadOnlyCollection<int> _skipDeserializationForStatusCodes = new ReadOnlyCollection<int>(new int[] { });
		private TimeSpan? _sniffLifeSpan;
		private bool _sniffOnConnectionFault;
		private bool _sniffOnStartup;
		private bool _throwExceptions;
		private bool _transferEncodingChunked;
		private IMemoryStreamFactory _memoryStreamFactory;
		private bool _enableTcpStats;
		private bool _enableThreadPoolStats;
		private UserAgent _userAgent;

		private Func<HttpMethod, int, bool> _statusCodeToResponseSuccess;

		/// <summary>
		/// <inheritdoc cref="TransportConfiguration"/>
		/// </summary>
		/// <param name="connectionPool"><inheritdoc cref="IConnectionPool"/></param>
		/// <param name="connection"><inheritdoc cref="IConnection"/></param>
		/// <param name="requestResponseSerializer"><inheritdoc cref="ITransportSerializer"/></param>
		/// <param name="productRegistration"><inheritdoc cref="IProductRegistration"/></param>
		protected TransportConfigurationBase(IConnectionPool connectionPool, IConnection connection, ITransportSerializer requestResponseSerializer, IProductRegistration productRegistration)
		{
			_connectionPool = connectionPool;
			_connection = connection ?? new HttpConnection();
			_productRegistration = productRegistration ?? ProductRegistration.Default;
			var serializer = requestResponseSerializer ?? new LowLevelRequestResponseSerializer();
			UseThisRequestResponseSerializer = new DiagnosticsSerializerProxy(serializer);

			_connectionLimit = TransportConfiguration.DefaultConnectionLimit;
			_requestTimeout = TransportConfiguration.DefaultTimeout;
			_dnsRefreshTimeout = TransportConfiguration.DefaultDnsRefreshTimeout;
			_memoryStreamFactory = TransportConfiguration.DefaultMemoryStreamFactory;
			_sniffOnConnectionFault = true;
			_sniffOnStartup = true;
			_sniffLifeSpan = TimeSpan.FromHours(1);

			_urlFormatter = new UrlFormatter(this);
			_statusCodeToResponseSuccess = (m, i) => _productRegistration.HttpStatusCodeClassifier(m, i);
			_userAgent = Elastic.Transport.UserAgent.Create(_productRegistration.Name, _productRegistration.GetType());

			if (connectionPool is CloudConnectionPool cloudPool)
			{
				_basicAuthCredentials = cloudPool.BasicCredentials;
				_apiKeyAuthCredentials = cloudPool.ApiKeyCredentials;
				_enableHttpCompression = true;
			}

		}

		/// <summary>
		/// Allows more specialized implementations of <see cref="TransportConfigurationBase{T}"/> to use their own
		/// request response serializer defaults
		/// </summary>
		// ReSharper disable once MemberCanBePrivate.Global
		// ReSharper disable once AutoPropertyCanBeMadeGetOnly.Global
		protected ITransportSerializer UseThisRequestResponseSerializer { get; set; }

		BasicAuthenticationCredentials ITransportConfigurationValues.BasicAuthenticationCredentials => _basicAuthCredentials;
		ApiKeyAuthenticationCredentials ITransportConfigurationValues.ApiKeyAuthenticationCredentials => _apiKeyAuthCredentials;
		SemaphoreSlim ITransportConfigurationValues.BootstrapLock => _semaphore;
		X509CertificateCollection ITransportConfigurationValues.ClientCertificates => _clientCertificates;
		IConnection ITransportConfigurationValues.Connection => _connection;
		IProductRegistration ITransportConfigurationValues.ProductRegistration => _productRegistration;
		int ITransportConfigurationValues.ConnectionLimit => _connectionLimit;
		IConnectionPool ITransportConfigurationValues.ConnectionPool => _connectionPool;
		TimeSpan? ITransportConfigurationValues.DeadTimeout => _deadTimeout;
		bool ITransportConfigurationValues.DisableAutomaticProxyDetection => _disableAutomaticProxyDetection;
		bool ITransportConfigurationValues.DisableDirectStreaming => _disableDirectStreaming;
		bool ITransportConfigurationValues.DisablePings => _disablePings;
		bool ITransportConfigurationValues.EnableHttpCompression => _enableHttpCompression;
		NameValueCollection ITransportConfigurationValues.Headers => _headers;
		bool ITransportConfigurationValues.HttpPipeliningEnabled => _enableHttpPipelining;
		TimeSpan? ITransportConfigurationValues.KeepAliveInterval => _keepAliveInterval;
		TimeSpan? ITransportConfigurationValues.KeepAliveTime => _keepAliveTime;
		TimeSpan? ITransportConfigurationValues.MaxDeadTimeout => _maxDeadTimeout;
		int? ITransportConfigurationValues.MaxRetries => _maxRetries;
		TimeSpan? ITransportConfigurationValues.MaxRetryTimeout => _maxRetryTimeout;
		IMemoryStreamFactory ITransportConfigurationValues.MemoryStreamFactory => _memoryStreamFactory;

		Func<Node, bool> ITransportConfigurationValues.NodePredicate => _nodePredicate;
		Action<IApiCallDetails> ITransportConfigurationValues.OnRequestCompleted => _completedRequestHandler;
		Action<RequestData> ITransportConfigurationValues.OnRequestDataCreated => _onRequestDataCreated;
		TimeSpan? ITransportConfigurationValues.PingTimeout => _pingTimeout;
		string ITransportConfigurationValues.ProxyAddress => _proxyAddress;
		SecureString ITransportConfigurationValues.ProxyPassword => _proxyPassword;
		string ITransportConfigurationValues.ProxyUsername => _proxyUsername;
		NameValueCollection ITransportConfigurationValues.QueryStringParameters => _queryString;
		ITransportSerializer ITransportConfigurationValues.RequestResponseSerializer => UseThisRequestResponseSerializer;
		TimeSpan ITransportConfigurationValues.RequestTimeout => _requestTimeout;
		TimeSpan ITransportConfigurationValues.DnsRefreshTimeout => _dnsRefreshTimeout;

		Func<object, X509Certificate, X509Chain, SslPolicyErrors, bool> ITransportConfigurationValues.ServerCertificateValidationCallback =>
			_serverCertificateValidationCallback;

		IReadOnlyCollection<int> ITransportConfigurationValues.SkipDeserializationForStatusCodes => _skipDeserializationForStatusCodes;
		TimeSpan? ITransportConfigurationValues.SniffInformationLifeSpan => _sniffLifeSpan;
		bool ITransportConfigurationValues.SniffsOnConnectionFault => _sniffOnConnectionFault;
		bool ITransportConfigurationValues.SniffsOnStartup => _sniffOnStartup;
		bool ITransportConfigurationValues.ThrowExceptions => _throwExceptions;
		UrlFormatter ITransportConfigurationValues.UrlFormatter => _urlFormatter;
		UserAgent ITransportConfigurationValues.UserAgent => _userAgent;
		Func<HttpMethod, int, bool> ITransportConfigurationValues.StatusCodeToResponseSuccess => _statusCodeToResponseSuccess;
		bool ITransportConfigurationValues.TransferEncodingChunked => _transferEncodingChunked;
		bool ITransportConfigurationValues.EnableTcpStats => _enableTcpStats;
		bool ITransportConfigurationValues.EnableThreadPoolStats => _enableThreadPoolStats;

		void IDisposable.Dispose() => DisposeManagedResources();

		private static void DefaultCompletedRequestHandler(IApiCallDetails response) { }

		private static void DefaultRequestDataCreated(RequestData response) { }

		/// <summary> Assign a private value and return the current <typeparamref name="T"/> </summary>
		// ReSharper disable once MemberCanBePrivate.Global
		protected T Assign<TValue>(TValue value, Action<T, TValue> assigner) => Fluent.Assign((T)this, value, assigner);

		/// <summary>
		/// Sets the keep-alive option on a TCP connection.
		/// <para>For Desktop CLR, sets ServicePointManager.SetTcpKeepAlive</para>
		/// </summary>
		/// <param name="keepAliveTime"><inheritdoc cref="ITransportConfigurationValues.KeepAliveTime"/></param>
		/// <param name="keepAliveInterval"><inheritdoc cref="ITransportConfigurationValues.KeepAliveInterval"/></param>
		public T EnableTcpKeepAlive(TimeSpan keepAliveTime, TimeSpan keepAliveInterval) =>
			Assign(keepAliveTime, (a, v) => a._keepAliveTime = v)
			.Assign(keepAliveInterval, (a, v) => a._keepAliveInterval = v);

		/// <inheritdoc cref="ITransportConfigurationValues.MaxRetries"/>
		public T MaximumRetries(int maxRetries) => Assign(maxRetries, (a, v) => a._maxRetries = v);

		/// <summary>
		/// <inheritdoc cref="ITransportConfigurationValues.ConnectionLimit"/>
		/// </summary>
		/// <param name="connectionLimit">The connection limit, a value lower then 0 will cause the connection limit not to be set at all</param>
		public T ConnectionLimit(int connectionLimit) => Assign(connectionLimit, (a, v) => a._connectionLimit = v);

		/// <inheritdoc cref="ITransportConfigurationValues.SniffsOnConnectionFault"/>
		public T SniffOnConnectionFault(bool sniffsOnConnectionFault = true) =>
			Assign(sniffsOnConnectionFault, (a, v) => a._sniffOnConnectionFault = v);

		/// <inheritdoc cref="ITransportConfigurationValues.SniffsOnStartup"/>
		public T SniffOnStartup(bool sniffsOnStartup = true) => Assign(sniffsOnStartup, (a, v) => a._sniffOnStartup = v);

		/// <summary>
		/// <inheritdoc cref="ITransportConfigurationValues.SniffInformationLifeSpan"/>
		/// </summary>
		/// <param name="sniffLifeSpan">The duration a clusterstate is considered fresh, set to null to disable periodic sniffing</param>
		public T SniffLifeSpan(TimeSpan? sniffLifeSpan) => Assign(sniffLifeSpan, (a, v) => a._sniffLifeSpan = v);

		/// <inheritdoc cref="ITransportConfigurationValues.EnableHttpCompression"/>
		public T EnableHttpCompression(bool enabled = true) => Assign(enabled, (a, v) => a._enableHttpCompression = v);

		/// <inheritdoc cref="ITransportConfigurationValues.DisableAutomaticProxyDetection"/>
		public T DisableAutomaticProxyDetection(bool disable = true) => Assign(disable, (a, v) => a._disableAutomaticProxyDetection = v);

		/// <inheritdoc cref="ITransportConfigurationValues.ThrowExceptions"/>
		public T ThrowExceptions(bool alwaysThrow = true) => Assign(alwaysThrow, (a, v) => a._throwExceptions = v);

		/// <inheritdoc cref="ITransportConfigurationValues.DisablePings"/>
		public T DisablePing(bool disable = true) => Assign(disable, (a, v) => a._disablePings = v);

		/// <inheritdoc cref="ITransportConfigurationValues.QueryStringParameters"/>
		// ReSharper disable once MemberCanBePrivate.Global
		public T GlobalQueryStringParameters(NameValueCollection queryStringParameters) => Assign(queryStringParameters, (a, v) => a._queryString.Add(v));

		/// <inheritdoc cref="ITransportConfigurationValues.Headers"/>
		public T GlobalHeaders(NameValueCollection headers) => Assign(headers, (a, v) => a._headers.Add(v));

		/// <inheritdoc cref="ITransportConfigurationValues.RequestTimeout"/>
		public T RequestTimeout(TimeSpan timeout) => Assign(timeout, (a, v) => a._requestTimeout = v);

		/// <inheritdoc cref="ITransportConfigurationValues.PingTimeout"/>
		public T PingTimeout(TimeSpan timeout) => Assign(timeout, (a, v) => a._pingTimeout = v);

		/// <inheritdoc cref="ITransportConfigurationValues.DeadTimeout"/>
		public T DeadTimeout(TimeSpan timeout) => Assign(timeout, (a, v) => a._deadTimeout = v);

		/// <inheritdoc cref="ITransportConfigurationValues.MaxDeadTimeout"/>
		public T MaxDeadTimeout(TimeSpan timeout) => Assign(timeout, (a, v) => a._maxDeadTimeout = v);

		/// <inheritdoc cref="ITransportConfigurationValues.MaxRetryTimeout"/>
		public T MaxRetryTimeout(TimeSpan maxRetryTimeout) => Assign(maxRetryTimeout, (a, v) => a._maxRetryTimeout = v);

		/// <inheritdoc cref="ITransportConfigurationValues.DnsRefreshTimeout"/>
		public T DnsRefreshTimeout(TimeSpan timeout) => Assign(timeout, (a, v) => a._dnsRefreshTimeout = v);

		/// <summary>
		/// If your connection has to go through proxy, use this method to specify the proxy url
		/// </summary>
		public T Proxy(Uri proxyAddress, string username, string password) =>
			Assign(proxyAddress.ToString(), (a, v) => a._proxyAddress = v)
				.Assign(username, (a, v) => a._proxyUsername = v)
				.Assign(password, (a, v) => a._proxyPassword = v.CreateSecureString());

		/// <inheritdoc cref="Proxy(System.Uri,string,string)"/>>
		public T Proxy(Uri proxyAddress, string username, SecureString password) =>
			Assign(proxyAddress.ToString(), (a, v) => a._proxyAddress = v)
				.Assign(username, (a, v) => a._proxyUsername = v)
				.Assign(password, (a, v) => a._proxyPassword = v);

		/// <inheritdoc cref="ITransportConfigurationValues.DisableDirectStreaming"/>
		// ReSharper disable once MemberCanBePrivate.Global
		public T DisableDirectStreaming(bool b = true) => Assign(b, (a, v) => a._disableDirectStreaming = v);

		/// <inheritdoc cref="ITransportConfigurationValues.OnRequestCompleted"/>
		public T OnRequestCompleted(Action<IApiCallDetails> handler) =>
			Assign(handler, (a, v) => a._completedRequestHandler += v ?? DefaultCompletedRequestHandler);

		/// <inheritdoc cref="ITransportConfigurationValues.OnRequestDataCreated"/>
		public T OnRequestDataCreated(Action<RequestData> handler) =>
			Assign(handler, (a, v) => a._onRequestDataCreated += v ?? DefaultRequestDataCreated);

		/// <inheritdoc cref="ITransportConfigurationValues.BasicAuthenticationCredentials"/>
		public T BasicAuthentication(string username, string password) =>
			Assign(new BasicAuthenticationCredentials(username, password), (a, v) => a._basicAuthCredentials = v);

		/// <inheritdoc cref="ITransportConfigurationValues.BasicAuthenticationCredentials"/>
		public T BasicAuthentication(string username, SecureString password) =>
			Assign(new BasicAuthenticationCredentials(username, password), (a, v) => a._basicAuthCredentials = v);

		/// <inheritdoc cref="ITransportConfigurationValues.ApiKeyAuthenticationCredentials"/>
		public T ApiKeyAuthentication(string id, SecureString apiKey) =>
			Assign(new ApiKeyAuthenticationCredentials(id, apiKey), (a, v) => a._apiKeyAuthCredentials = v);

		/// <inheritdoc cref="ITransportConfigurationValues.ApiKeyAuthenticationCredentials"/>
		public T ApiKeyAuthentication(string id, string apiKey) =>
			Assign(new ApiKeyAuthenticationCredentials(id, apiKey), (a, v) => a._apiKeyAuthCredentials = v);

		/// <inheritdoc cref="ITransportConfigurationValues.ApiKeyAuthenticationCredentials"/>
		public T ApiKeyAuthentication(ApiKeyAuthenticationCredentials credentials) =>
			Assign(credentials, (a, v) => a._apiKeyAuthCredentials = v);

		/// <inheritdoc cref="ITransportConfigurationValues.HttpPipeliningEnabled"/>
		public T EnableHttpPipelining(bool enabled = true) => Assign(enabled, (a, v) => a._enableHttpPipelining = v);

		/// <summary>
		/// <inheritdoc cref="ITransportConfigurationValues.NodePredicate"/>
		/// </summary>
		/// <param name="predicate">Return true if you want the node to be used for API calls</param>
		public T NodePredicate(Func<Node, bool> predicate) => Assign(predicate, (a, v) => a._nodePredicate = v);

		/// <summary>
		/// Turns on settings that aid in debugging like DisableDirectStreaming() and PrettyJson()
		/// so that the original request and response JSON can be inspected. It also always asks the server for the full stack trace on errors
		/// </summary>
		/// <param name="onRequestCompleted">
		/// An optional callback to be performed when the request completes. This will
		/// not overwrite the global OnRequestCompleted callback that is set directly on
		/// ConnectionSettings. If no callback is passed, DebugInformation from the response
		/// will be written to the debug output by default.
		/// </param>
		// ReSharper disable once VirtualMemberNeverOverridden.Global
		public virtual T EnableDebugMode(Action<IApiCallDetails> onRequestCompleted = null) =>
			PrettyJson()
				.DisableDirectStreaming()
				.EnableTcpStats()
				.EnableThreadPoolStats()
				.Assign(onRequestCompleted, (a, v) =>
					_completedRequestHandler += v ?? (d => Debug.WriteLine(d.DebugInformation)));

		private bool _prettyJson;
		bool ITransportConfigurationValues.PrettyJson => _prettyJson;

		/// <inheritdoc cref="ITransportConfigurationValues.PrettyJson"/>
		// ReSharper disable once VirtualMemberNeverOverridden.Global
		// ReSharper disable once MemberCanBeProtected.Global
		public virtual T PrettyJson(bool b = true) => Assign(b, (a, v) => a._prettyJson = v);

		/// <inheritdoc cref="ITransportConfigurationValues.ServerCertificateValidationCallback"/>
		public T ServerCertificateValidationCallback(Func<object, X509Certificate, X509Chain, SslPolicyErrors, bool> callback) =>
			Assign(callback, (a, v) => a._serverCertificateValidationCallback = v);

		/// <inheritdoc cref="ITransportConfigurationValues.ClientCertificates"/>
		public T ClientCertificates(X509CertificateCollection certificates) =>
			Assign(certificates, (a, v) => a._clientCertificates = v);

		/// <inheritdoc cref="ITransportConfigurationValues.ClientCertificates"/>
		public T ClientCertificate(X509Certificate certificate) =>
			Assign(new X509Certificate2Collection { certificate }, (a, v) => a._clientCertificates = v);

		/// <inheritdoc cref="ITransportConfigurationValues.ClientCertificates"/>
		public T ClientCertificate(string certificatePath) =>
			Assign(new X509Certificate2Collection { new X509Certificate(certificatePath) }, (a, v) => a._clientCertificates = v);

		/// <inheritdoc cref="ITransportConfigurationValues.SkipDeserializationForStatusCodes"/>
		public T SkipDeserializationForStatusCodes(params int[] statusCodes) =>
			Assign(new ReadOnlyCollection<int>(statusCodes), (a, v) => a._skipDeserializationForStatusCodes = v);

		/// <inheritdoc cref="ITransportConfigurationValues.UserAgent"/>
		public T UserAgent(UserAgent userAgent) => Assign(userAgent, (a, v) => a._userAgent = v);

		/// <inheritdoc cref="ITransportConfigurationValues.TransferEncodingChunked"/>
		public T TransferEncodingChunked(bool transferEncodingChunked = true) => Assign(transferEncodingChunked, (a, v) => a._transferEncodingChunked = v);

		/// <inheritdoc cref="ITransportConfigurationValues.MemoryStreamFactory"/>
		public T MemoryStreamFactory(IMemoryStreamFactory memoryStreamFactory) => Assign(memoryStreamFactory, (a, v) => a._memoryStreamFactory = v);

		/// <inheritdoc cref="ITransportConfigurationValues.EnableTcpStats"/>>
		public T EnableTcpStats(bool enableTcpStats = true) => Assign(enableTcpStats, (a, v) => a._enableTcpStats = v);

		/// <inheritdoc cref="ITransportConfigurationValues.EnableThreadPoolStats"/>>
		public T EnableThreadPoolStats(bool enableThreadPoolStats = true) => Assign(enableThreadPoolStats, (a, v) => a._enableThreadPoolStats = v);

		// ReSharper disable once VirtualMemberNeverOverridden.Global
		/// <summary> Allows subclasses to hook into the parents dispose </summary>
		protected virtual void DisposeManagedResources()
		{
			_connectionPool?.Dispose();
			_connection?.Dispose();
			_semaphore?.Dispose();
			_proxyPassword?.Dispose();
			_basicAuthCredentials?.Dispose();
			_apiKeyAuthCredentials?.Dispose();
		}

		/// <summary> Allows subclasses to add/remove default global query string parameters </summary>
		protected T UpdateGlobalQueryString(string key, string value, bool enabled)
		{
			if (!enabled && _queryString[key] != null) _queryString.Remove(key);
			else if (enabled && _queryString[key] == null)
				return GlobalQueryStringParameters(new NameValueCollection { { key, "true" } });
			return (T)this;
		}

	}
}
