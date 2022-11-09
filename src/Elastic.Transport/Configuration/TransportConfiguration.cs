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
	/// Allows you to control how <see cref="HttpTransport{TConnectionSettings}"/> behaves and where/how it connects to Elastic Stack products
	/// </summary>
	public class TransportConfiguration : TransportConfigurationBase<TransportConfiguration>
	{
		/// <summary>
		/// Detects whether we are running on .NET Core with CurlHandler.
		/// If this is true, we will set a very restrictive <see cref="DefaultConnectionLimit"/>
		/// As the old curl based handler is known to bleed TCP connections:
		/// <para>https://github.com/dotnet/runtime/issues/22366</para>
		/// </summary>
		private static bool UsingCurlHandler => ConnectionInfo.UsingCurlHandler;

		//public static MemoryStreamFactory Default { get; } = RecyclableMemoryStreamFactory.Default;
		// ReSharper disable once RedundantNameQualifier
		/// <summary>
		/// The default memory stream factory if none is configured on <see cref="ITransportConfiguration.MemoryStreamFactory"/>
		/// </summary>
		public static MemoryStreamFactory DefaultMemoryStreamFactory { get; } = Elastic.Transport.DefaultMemoryStreamFactory.Default;

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
		/// <param name="productRegistration"><inheritdoc cref="IProductRegistration" path="/summary"/></param>
		public TransportConfiguration(Uri uri = null, IProductRegistration productRegistration = null)
			: this(new SingleNodePool(uri ?? new Uri("http://localhost:9200")), productRegistration: productRegistration) { }

		/// <summary>
		/// Sets up the client to communicate to Elastic Cloud using <paramref name="cloudId"/>,
		/// <para><see cref="CloudNodePool"/> documentation for more information on how to obtain your Cloud Id</para>
		/// </summary>
		public TransportConfiguration(string cloudId, BasicAuthentication credentials, IProductRegistration productRegistration = null)
			: this(new CloudNodePool(cloudId, credentials), productRegistration: productRegistration) { }

		/// <summary>
		/// Sets up the client to communicate to Elastic Cloud using <paramref name="cloudId"/>,
		/// <para><see cref="CloudNodePool"/> documentation for more information on how to obtain your Cloud Id</para>
		/// </summary>
		public TransportConfiguration(string cloudId, Base64ApiKey credentials, IProductRegistration productRegistration = null)
			: this(new CloudNodePool(cloudId, credentials), productRegistration: productRegistration) { }

		/// <summary> <inheritdoc cref="TransportConfiguration" path="/summary"/></summary>
		/// <param name="nodePool"><inheritdoc cref="NodePool" path="/summary"/></param>
		/// <param name="connection"><inheritdoc cref="TransportClient" path="/summary"/></param>
		/// <param name="serializer"><inheritdoc cref="Serializer" path="/summary"/></param>
		/// <param name="productRegistration"><inheritdoc cref="IProductRegistration" path="/summary"/></param>
		public TransportConfiguration(
			NodePool nodePool,
			TransportClient connection = null,
			Serializer serializer = null,
			IProductRegistration productRegistration = null)
			: base(nodePool, connection, serializer, productRegistration) { }

	}

	/// <inheritdoc cref="TransportConfiguration"/>>
	[Browsable(false)]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public abstract class TransportConfigurationBase<T> : ITransportConfiguration
		where T : TransportConfigurationBase<T>
	{
		private readonly TransportClient _transportClient;
		private readonly NodePool _nodePool;
		private readonly IProductRegistration _productRegistration;
		private readonly NameValueCollection _headers = new NameValueCollection();
		private readonly NameValueCollection _queryString = new NameValueCollection();
		private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
		private readonly UrlFormatter _urlFormatter;

		private AuthorizationHeader _authenticationHeader;
		private X509CertificateCollection _clientCertificates;
		private Action<ApiCallDetails> _completedRequestHandler = DefaultCompletedRequestHandler;
		private int _transportClientLimit;
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
		private string _proxyPassword;
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
		private MemoryStreamFactory _memoryStreamFactory;
		private bool _enableTcpStats;
		private bool _enableThreadPoolStats;
		private UserAgent _userAgent;
		private string _certificateFingerprint;
		private bool _disableMetaHeader;
		private readonly MetaHeaderProvider _metaHeaderProvider;

		private readonly Func<HttpMethod, int, bool> _statusCodeToResponseSuccess;

		/// <summary>
		/// <inheritdoc cref="TransportConfiguration"/>
		/// </summary>
		/// <param name="nodePool"><inheritdoc cref="NodePool" path="/summary"/></param>
		/// <param name="transportClient"><inheritdoc cref="TransportClient" path="/summary"/></param>
		/// <param name="requestResponseSerializer"><inheritdoc cref="Serializer" path="/summary"/></param>
		/// <param name="productRegistration"><inheritdoc cref="IProductRegistration" path="/summary"/></param>
		protected TransportConfigurationBase(NodePool nodePool, TransportClient transportClient, Serializer requestResponseSerializer, IProductRegistration productRegistration)
		{
			_nodePool = nodePool;
			_transportClient = transportClient ?? new HttpTransportClient();
			_productRegistration = productRegistration ?? ProductRegistration.Default;
			var serializer = requestResponseSerializer ?? new LowLevelRequestResponseSerializer();
			UseThisRequestResponseSerializer = new DiagnosticsSerializerProxy(serializer);

			_transportClientLimit = TransportConfiguration.DefaultConnectionLimit;
			_requestTimeout = TransportConfiguration.DefaultTimeout;
			_dnsRefreshTimeout = TransportConfiguration.DefaultDnsRefreshTimeout;
			_memoryStreamFactory = TransportConfiguration.DefaultMemoryStreamFactory;
			_sniffOnConnectionFault = true;
			_sniffOnStartup = true;
			_sniffLifeSpan = TimeSpan.FromHours(1);

			_metaHeaderProvider = productRegistration?.MetaHeaderProvider;

			_urlFormatter = new UrlFormatter(this);
			_statusCodeToResponseSuccess = (m, i) => _productRegistration.HttpStatusCodeClassifier(m, i);
			_userAgent = Elastic.Transport.UserAgent.Create(_productRegistration.Name, _productRegistration.GetType());

			if (nodePool is CloudNodePool cloudPool)
			{
				_authenticationHeader = cloudPool.AuthenticationHeader;
				_enableHttpCompression = true;
			}

			_headersToParse = new HeadersList(_productRegistration.ResponseHeadersToParse);
		}

		/// <summary>
		/// Allows more specialized implementations of <see cref="TransportConfigurationBase{T}"/> to use their own
		/// request response serializer defaults
		/// </summary>
		// ReSharper disable once MemberCanBePrivate.Global
		// ReSharper disable once AutoPropertyCanBeMadeGetOnly.Global
		protected Serializer UseThisRequestResponseSerializer { get; set; }

		AuthorizationHeader ITransportConfiguration.Authentication => _authenticationHeader;
		SemaphoreSlim ITransportConfiguration.BootstrapLock => _semaphore;
		X509CertificateCollection ITransportConfiguration.ClientCertificates => _clientCertificates;
		TransportClient ITransportConfiguration.Connection => _transportClient;
		IProductRegistration ITransportConfiguration.ProductRegistration => _productRegistration;
		int ITransportConfiguration.ConnectionLimit => _transportClientLimit;
		NodePool ITransportConfiguration.NodePool => _nodePool;
		TimeSpan? ITransportConfiguration.DeadTimeout => _deadTimeout;
		bool ITransportConfiguration.DisableAutomaticProxyDetection => _disableAutomaticProxyDetection;
		bool ITransportConfiguration.DisableDirectStreaming => _disableDirectStreaming;
		bool ITransportConfiguration.DisablePings => _disablePings;
		bool ITransportConfiguration.EnableHttpCompression => _enableHttpCompression;
		NameValueCollection ITransportConfiguration.Headers => _headers;
		bool ITransportConfiguration.HttpPipeliningEnabled => _enableHttpPipelining;
		TimeSpan? ITransportConfiguration.KeepAliveInterval => _keepAliveInterval;
		TimeSpan? ITransportConfiguration.KeepAliveTime => _keepAliveTime;
		TimeSpan? ITransportConfiguration.MaxDeadTimeout => _maxDeadTimeout;
		int? ITransportConfiguration.MaxRetries => _maxRetries;
		TimeSpan? ITransportConfiguration.MaxRetryTimeout => _maxRetryTimeout;
		MemoryStreamFactory ITransportConfiguration.MemoryStreamFactory => _memoryStreamFactory;

		Func<Node, bool> ITransportConfiguration.NodePredicate => _nodePredicate;
		Action<ApiCallDetails> ITransportConfiguration.OnRequestCompleted => _completedRequestHandler;
		Action<RequestData> ITransportConfiguration.OnRequestDataCreated => _onRequestDataCreated;
		TimeSpan? ITransportConfiguration.PingTimeout => _pingTimeout;
		string ITransportConfiguration.ProxyAddress => _proxyAddress;
		string ITransportConfiguration.ProxyPassword => _proxyPassword;
		string ITransportConfiguration.ProxyUsername => _proxyUsername;
		NameValueCollection ITransportConfiguration.QueryStringParameters => _queryString;
		Serializer ITransportConfiguration.RequestResponseSerializer => UseThisRequestResponseSerializer;
		TimeSpan ITransportConfiguration.RequestTimeout => _requestTimeout;
		TimeSpan ITransportConfiguration.DnsRefreshTimeout => _dnsRefreshTimeout;
		string ITransportConfiguration.CertificateFingerprint => _certificateFingerprint;

		Func<object, X509Certificate, X509Chain, SslPolicyErrors, bool> ITransportConfiguration.ServerCertificateValidationCallback =>
			_serverCertificateValidationCallback;

		IReadOnlyCollection<int> ITransportConfiguration.SkipDeserializationForStatusCodes => _skipDeserializationForStatusCodes;
		TimeSpan? ITransportConfiguration.SniffInformationLifeSpan => _sniffLifeSpan;
		bool ITransportConfiguration.SniffsOnConnectionFault => _sniffOnConnectionFault;
		bool ITransportConfiguration.SniffsOnStartup => _sniffOnStartup;
		bool ITransportConfiguration.ThrowExceptions => _throwExceptions;
		UrlFormatter ITransportConfiguration.UrlFormatter => _urlFormatter;
		UserAgent ITransportConfiguration.UserAgent => _userAgent;
		Func<HttpMethod, int, bool> ITransportConfiguration.StatusCodeToResponseSuccess => _statusCodeToResponseSuccess;
		bool ITransportConfiguration.TransferEncodingChunked => _transferEncodingChunked;
		bool ITransportConfiguration.EnableTcpStats => _enableTcpStats;
		bool ITransportConfiguration.EnableThreadPoolStats => _enableThreadPoolStats;

		void IDisposable.Dispose() => DisposeManagedResources();

		private static void DefaultCompletedRequestHandler(ApiCallDetails response) { }

		private static void DefaultRequestDataCreated(RequestData response) { }

		/// <summary> Assign a private value and return the current <typeparamref name="T"/> </summary>
		// ReSharper disable once MemberCanBePrivate.Global
		protected T Assign<TValue>(TValue value, Action<T, TValue> assigner) => Fluent.Assign((T)this, value, assigner);

		/// <summary>
		/// Sets the keep-alive option on a TCP connection.
		/// <para>For Desktop CLR, sets ServicePointManager.SetTcpKeepAlive</para>
		/// </summary>
		/// <param name="keepAliveTime"><inheritdoc cref="ITransportConfiguration.KeepAliveTime" path="/summary"/></param>
		/// <param name="keepAliveInterval"><inheritdoc cref="ITransportConfiguration.KeepAliveInterval" path="/summary"/></param>
		public T EnableTcpKeepAlive(TimeSpan keepAliveTime, TimeSpan keepAliveInterval) =>
			Assign(keepAliveTime, (a, v) => a._keepAliveTime = v)
			.Assign(keepAliveInterval, (a, v) => a._keepAliveInterval = v);

		/// <inheritdoc cref="ITransportConfiguration.MaxRetries"/>
		public T MaximumRetries(int maxRetries) => Assign(maxRetries, (a, v) => a._maxRetries = v);

		/// <summary>
		/// <inheritdoc cref="ITransportConfiguration.ConnectionLimit" path="/summary"/>
		/// </summary>
		/// <param name="connectionLimit">The connection limit, a value lower then 0 will cause the connection limit not to be set at all</param>
		public T ConnectionLimit(int connectionLimit) => Assign(connectionLimit, (a, v) => a._transportClientLimit = v);

		/// <inheritdoc cref="ITransportConfiguration.SniffsOnConnectionFault"/>
		public T SniffOnConnectionFault(bool sniffsOnConnectionFault = true) =>
			Assign(sniffsOnConnectionFault, (a, v) => a._sniffOnConnectionFault = v);

		/// <inheritdoc cref="ITransportConfiguration.SniffsOnStartup"/>
		public T SniffOnStartup(bool sniffsOnStartup = true) => Assign(sniffsOnStartup, (a, v) => a._sniffOnStartup = v);

		/// <summary>
		/// <inheritdoc cref="ITransportConfiguration.SniffInformationLifeSpan" path="/summary"/>
		/// </summary>
		/// <param name="sniffLifeSpan">The duration a clusterstate is considered fresh, set to null to disable periodic sniffing</param>
		public T SniffLifeSpan(TimeSpan? sniffLifeSpan) => Assign(sniffLifeSpan, (a, v) => a._sniffLifeSpan = v);

		/// <inheritdoc cref="ITransportConfiguration.EnableHttpCompression"/>
		public T EnableHttpCompression(bool enabled = true) => Assign(enabled, (a, v) => a._enableHttpCompression = v);

		/// <inheritdoc cref="ITransportConfiguration.DisableAutomaticProxyDetection"/>
		public T DisableAutomaticProxyDetection(bool disable = true) => Assign(disable, (a, v) => a._disableAutomaticProxyDetection = v);

		/// <inheritdoc cref="ITransportConfiguration.ThrowExceptions"/>
		public T ThrowExceptions(bool alwaysThrow = true) => Assign(alwaysThrow, (a, v) => a._throwExceptions = v);

		/// <inheritdoc cref="ITransportConfiguration.DisablePings"/>
		public T DisablePing(bool disable = true) => Assign(disable, (a, v) => a._disablePings = v);

		/// <inheritdoc cref="ITransportConfiguration.QueryStringParameters"/>
		// ReSharper disable once MemberCanBePrivate.Global
		public T GlobalQueryStringParameters(NameValueCollection queryStringParameters) => Assign(queryStringParameters, (a, v) => a._queryString.Add(v));

		/// <inheritdoc cref="ITransportConfiguration.Headers"/>
		public T GlobalHeaders(NameValueCollection headers) => Assign(headers, (a, v) => a._headers.Add(v));

		/// <inheritdoc cref="ITransportConfiguration.RequestTimeout"/>
		public T RequestTimeout(TimeSpan timeout) => Assign(timeout, (a, v) => a._requestTimeout = v);

		/// <inheritdoc cref="ITransportConfiguration.PingTimeout"/>
		public T PingTimeout(TimeSpan timeout) => Assign(timeout, (a, v) => a._pingTimeout = v);

		/// <inheritdoc cref="ITransportConfiguration.DeadTimeout"/>
		public T DeadTimeout(TimeSpan timeout) => Assign(timeout, (a, v) => a._deadTimeout = v);

		/// <inheritdoc cref="ITransportConfiguration.MaxDeadTimeout"/>
		public T MaxDeadTimeout(TimeSpan timeout) => Assign(timeout, (a, v) => a._maxDeadTimeout = v);

		/// <inheritdoc cref="ITransportConfiguration.MaxRetryTimeout"/>
		public T MaxRetryTimeout(TimeSpan maxRetryTimeout) => Assign(maxRetryTimeout, (a, v) => a._maxRetryTimeout = v);

		/// <inheritdoc cref="ITransportConfiguration.DnsRefreshTimeout"/>
		public T DnsRefreshTimeout(TimeSpan timeout) => Assign(timeout, (a, v) => a._dnsRefreshTimeout = v);

		/// <inheritdoc cref="ITransportConfiguration.CertificateFingerprint"/>
		public T CertificateFingerprint(string fingerprint) => Assign(fingerprint, (a, v) => a._certificateFingerprint = v);

		/// <summary>
		/// If your connection has to go through proxy, use this method to specify the proxy url
		/// </summary>
		public T Proxy(Uri proxyAddress, string username, string password) =>
			Assign(proxyAddress.ToString(), (a, v) => a._proxyAddress = v)
				.Assign(username, (a, v) => a._proxyUsername = v)
				.Assign(password, (a, v) => a._proxyPassword = v);

		/// <inheritdoc cref="ITransportConfiguration.DisableDirectStreaming"/>
		// ReSharper disable once MemberCanBePrivate.Global
		public T DisableDirectStreaming(bool b = true) => Assign(b, (a, v) => a._disableDirectStreaming = v);

		/// <inheritdoc cref="ITransportConfiguration.OnRequestCompleted"/>
		public T OnRequestCompleted(Action<ApiCallDetails> handler) =>
			Assign(handler, (a, v) => a._completedRequestHandler += v ?? DefaultCompletedRequestHandler);

		/// <inheritdoc cref="ITransportConfiguration.OnRequestDataCreated"/>
		public T OnRequestDataCreated(Action<RequestData> handler) =>
			Assign(handler, (a, v) => a._onRequestDataCreated += v ?? DefaultRequestDataCreated);

		/// <inheritdoc cref="AuthorizationHeader"/>
		public T Authentication(AuthorizationHeader header) => Assign(header, (a, v) => a._authenticationHeader = v);

		/// <inheritdoc cref="ITransportConfiguration.HttpPipeliningEnabled"/>
		public T EnableHttpPipelining(bool enabled = true) => Assign(enabled, (a, v) => a._enableHttpPipelining = v);

		/// <summary>
		/// <inheritdoc cref="ITransportConfiguration.NodePredicate"/>
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
		public virtual T EnableDebugMode(Action<ApiCallDetails> onRequestCompleted = null) =>
			PrettyJson()
				.DisableDirectStreaming()
				.EnableTcpStats()
				.EnableThreadPoolStats()
				.Assign(onRequestCompleted, (a, v) =>
					_completedRequestHandler += v ?? (d => Debug.WriteLine(d.DebugInformation)));

		private bool _prettyJson;
		bool ITransportConfiguration.PrettyJson => _prettyJson;

		/// <inheritdoc cref="ITransportConfiguration.PrettyJson"/>
		// ReSharper disable once VirtualMemberNeverOverridden.Global
		// ReSharper disable once MemberCanBeProtected.Global
		public virtual T PrettyJson(bool b = true) => Assign(b, (a, v) => a._prettyJson = v);

		private bool? _parseAllHeaders;
		bool? ITransportConfiguration.ParseAllHeaders => _parseAllHeaders;

		/// <inheritdoc cref="ITransportConfiguration.ParseAllHeaders"/>
		public virtual T ParseAllHeaders(bool b = true) => Assign(b, (a, v) => a._parseAllHeaders = v);

		private HeadersList _headersToParse;
		HeadersList ITransportConfiguration.ResponseHeadersToParse => _headersToParse;

		MetaHeaderProvider ITransportConfiguration.MetaHeaderProvider => _metaHeaderProvider;

		bool ITransportConfiguration.DisableMetaHeader => _disableMetaHeader;

		/// <inheritdoc cref="ITransportConfiguration.ResponseHeadersToParse"/>
		public virtual T ResponseHeadersToParse(HeadersList headersToParse)
		{
			_headersToParse = new HeadersList(_headersToParse, headersToParse);
			return (T)this;
		}

		/// <inheritdoc cref="ITransportConfiguration.ServerCertificateValidationCallback"/>
		public T ServerCertificateValidationCallback(Func<object, X509Certificate, X509Chain, SslPolicyErrors, bool> callback) =>
			Assign(callback, (a, v) => a._serverCertificateValidationCallback = v);

		/// <inheritdoc cref="ITransportConfiguration.ClientCertificates"/>
		public T ClientCertificates(X509CertificateCollection certificates) =>
			Assign(certificates, (a, v) => a._clientCertificates = v);

		/// <inheritdoc cref="ITransportConfiguration.ClientCertificates"/>
		public T ClientCertificate(X509Certificate certificate) =>
			Assign(new X509Certificate2Collection { certificate }, (a, v) => a._clientCertificates = v);

		/// <inheritdoc cref="ITransportConfiguration.ClientCertificates"/>
		public T ClientCertificate(string certificatePath) =>
			Assign(new X509Certificate2Collection { new X509Certificate(certificatePath) }, (a, v) => a._clientCertificates = v);

		/// <inheritdoc cref="ITransportConfiguration.SkipDeserializationForStatusCodes"/>
		public T SkipDeserializationForStatusCodes(params int[] statusCodes) =>
			Assign(new ReadOnlyCollection<int>(statusCodes), (a, v) => a._skipDeserializationForStatusCodes = v);

		/// <inheritdoc cref="ITransportConfiguration.UserAgent"/>
		public T UserAgent(UserAgent userAgent) => Assign(userAgent, (a, v) => a._userAgent = v);

		/// <inheritdoc cref="ITransportConfiguration.TransferEncodingChunked"/>
		public T TransferEncodingChunked(bool transferEncodingChunked = true) => Assign(transferEncodingChunked, (a, v) => a._transferEncodingChunked = v);

		/// <inheritdoc cref="ITransportConfiguration.MemoryStreamFactory"/>
		public T MemoryStreamFactory(MemoryStreamFactory memoryStreamFactory) => Assign(memoryStreamFactory, (a, v) => a._memoryStreamFactory = v);

		/// <inheritdoc cref="ITransportConfiguration.EnableTcpStats"/>>
		public T EnableTcpStats(bool enableTcpStats = true) => Assign(enableTcpStats, (a, v) => a._enableTcpStats = v);

		/// <inheritdoc cref="ITransportConfiguration.EnableThreadPoolStats"/>>
		public T EnableThreadPoolStats(bool enableThreadPoolStats = true) => Assign(enableThreadPoolStats, (a, v) => a._enableThreadPoolStats = v);

		/// <inheritdoc cref="ITransportConfiguration.DisableMetaHeader"/>>
		public T DisableMetaHeader(bool disable = true) => Assign(disable, (a, v) => a._disableMetaHeader = v);

		// ReSharper disable once VirtualMemberNeverOverridden.Global
		/// <summary> Allows subclasses to hook into the parents dispose </summary>
		protected virtual void DisposeManagedResources()
		{
			_nodePool?.Dispose();
			_transportClient?.Dispose();
			_semaphore?.Dispose();
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
