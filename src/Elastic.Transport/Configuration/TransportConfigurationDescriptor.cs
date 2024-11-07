// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using Elastic.Transport.Extensions;
using Elastic.Transport.Products;

namespace Elastic.Transport;

/// <summary>
/// Allows you to control how <see cref="ITransport{TConfiguration}"/> behaves and where/how it connects to Elastic Stack products
/// </summary>
/// <remarks> <inheritdoc cref="TransportConfigurationDescriptor" path="/summary"/></remarks>
/// <param name="nodePool"><inheritdoc cref="NodePool" path="/summary"/></param>
/// <param name="invoker"><inheritdoc cref="IRequestInvoker" path="/summary"/></param>
/// <param name="serializer"><inheritdoc cref="Serializer" path="/summary"/></param>
/// <param name="productRegistration"><inheritdoc cref="ProductRegistration" path="/summary"/></param>
public class TransportConfigurationDescriptor(
	NodePool nodePool,
	IRequestInvoker? invoker = null,
	Serializer? serializer = null,
	ProductRegistration? productRegistration = null) : TransportConfigurationDescriptorBase<TransportConfigurationDescriptor>(nodePool, invoker, serializer, productRegistration)
{
	/// <summary>
	/// Creates a new instance of <see cref="TransportConfigurationDescriptor"/>
	/// </summary>
	/// <param name="uri">The root of the Elastic stack product node we want to connect to. Defaults to http://localhost:9200</param>
	/// <param name="productRegistration"><inheritdoc cref="ProductRegistration" path="/summary"/></param>
	public TransportConfigurationDescriptor(Uri? uri = null, ProductRegistration? productRegistration = null)
		: this(new SingleNodePool(uri ?? new Uri("http://localhost:9200")), productRegistration: productRegistration) { }

	/// <summary>
	/// Sets up the client to communicate to Elastic Cloud using <paramref name="cloudId"/>,
	/// <para><see cref="CloudNodePool"/> documentation for more information on how to obtain your Cloud Id</para>
	/// </summary>
	public TransportConfigurationDescriptor(string cloudId, BasicAuthentication credentials, ProductRegistration? productRegistration = null)
		: this(new CloudNodePool(cloudId, credentials), productRegistration: productRegistration) { }

	/// <summary>
	/// Sets up the client to communicate to Elastic Cloud using <paramref name="cloudId"/>,
	/// <para><see cref="CloudNodePool"/> documentation for more information on how to obtain your Cloud Id</para>
	/// </summary>
	public TransportConfigurationDescriptor(string cloudId, Base64ApiKey credentials, ProductRegistration? productRegistration = null)
		: this(new CloudNodePool(cloudId, credentials), productRegistration: productRegistration) { }
}

/// <inheritdoc cref="TransportConfigurationDescriptor"/>>
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public abstract class TransportConfigurationDescriptorBase<T> : ITransportConfiguration
	where T : TransportConfigurationDescriptorBase<T>
{
	/// <summary>
	/// <inheritdoc cref="TransportConfigurationDescriptor"/>
	/// </summary>
	/// <param name="nodePool"><inheritdoc cref="NodePool" path="/summary"/></param>
	/// <param name="requestInvoker"><inheritdoc cref="IRequestInvoker" path="/summary"/></param>
	/// <param name="requestResponseSerializer"><inheritdoc cref="Serializer" path="/summary"/></param>
	/// <param name="productRegistration"><inheritdoc cref="ProductRegistration" path="/summary"/></param>
	protected TransportConfigurationDescriptorBase(NodePool nodePool, IRequestInvoker? requestInvoker, Serializer? requestResponseSerializer, ProductRegistration? productRegistration)
	{
		_nodePool = nodePool;
		_requestInvoker = requestInvoker ?? new HttpRequestInvoker(this);
		_productRegistration = productRegistration ?? DefaultProductRegistration.Default;
		_requestInvoker = requestInvoker ?? new HttpRequestInvoker();
		_requestResponseSerializer = requestResponseSerializer ?? new LowLevelRequestResponseSerializer();
		_pipelineProvider = DefaultRequestPipelineFactory.Default;
		_dateTimeProvider = nodePool.DateTimeProvider;
		_bootstrapLock = new(1, 1);
		_metaHeaderProvider = productRegistration?.MetaHeaderProvider;
		_urlFormatter = new UrlFormatter(this);

		_accept = productRegistration?.DefaultContentType;
		_connectionLimit = TransportConfiguration.DefaultConnectionLimit;
		_dnsRefreshTimeout = TransportConfiguration.DefaultDnsRefreshTimeout;
		_memoryStreamFactory = TransportConfiguration.DefaultMemoryStreamFactory;
		_sniffsOnConnectionFault = true;
		_sniffsOnStartup = true;
		_sniffInformationLifeSpan = TimeSpan.FromHours(1);

		_statusCodeToResponseSuccess = _productRegistration.HttpStatusCodeClassifier;
		_userAgent = Transport.UserAgent.Create(_productRegistration.Name, _productRegistration.GetType());

		if (nodePool is CloudNodePool cloudPool)
		{
			_authentication = cloudPool.AuthenticationHeader;
			_enableHttpCompression = true;
		}

		_responseHeadersToParse = new HeadersList(_productRegistration.ResponseHeadersToParse);
	}

	private readonly SemaphoreSlim _bootstrapLock;
	private readonly NodePool _nodePool;
	private readonly ProductRegistration _productRegistration;

	//TODO these are not exposed globally
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
	private readonly IReadOnlyCollection<int>? _allowedStatusCodes;
	private readonly string? _contentType;
	private readonly bool? _disableSniff;
	private readonly Uri? _forceNode;
	private readonly string? _opaqueId;
	private readonly string? _runAs;
	private readonly RequestMetaData? _requestMetaData;
	private readonly IRequestInvoker? _requestInvoker;
#pragma warning restore CS0649 // Field is never assigned to, and will always have its default value

	private bool _prettyJson;
	private readonly string? _accept;
	private AuthorizationHeader? _authentication;
	private X509CertificateCollection? _clientCertificates;
	private bool? _disableDirectStreaming;
	private bool? _disableAuditTrail;
	private bool? _disablePings;
	private bool? _httpPipeliningEnabled;
	private bool? _enableHttpCompression;
	private int? _maxRetries;
	private TimeSpan? _maxRetryTimeout;
	private TimeSpan? _pingTimeout;
	private TimeSpan? _requestTimeout;
	private bool? _throwExceptions;
	private bool? _transferEncodingChunked;
	private NameValueCollection? _headers;
	private bool? _enableTcpStats;
	private bool? _enableThreadPoolStats;
	private int _connectionLimit;
	private TimeSpan? _deadTimeout;
	private bool _disableAutomaticProxyDetection;
	private TimeSpan? _keepAliveInterval;
	private TimeSpan? _keepAliveTime;
	private TimeSpan? _maxDeadTimeout;
	private MemoryStreamFactory _memoryStreamFactory;
	private Func<Node, bool>? _nodePredicate;
	private Action<ApiCallDetails>? _onRequestCompleted;
	private Action<RequestData>? _onRequestDataCreated;
	private string? _proxyAddress;
	private string? _proxyPassword;
	private string? _proxyUsername;
	private NameValueCollection? _queryStringParameters;
	private Serializer _requestResponseSerializer;
	private Func<object, X509Certificate, X509Chain, SslPolicyErrors, bool>? _serverCertificateValidationCallback;
	private string? _certificateFingerprint;
	private IReadOnlyCollection<int>? _skipDeserializationForStatusCodes;
	private TimeSpan? _sniffInformationLifeSpan;
	private bool _sniffsOnConnectionFault;
	private bool _sniffsOnStartup;
	private readonly UrlFormatter _urlFormatter;
	private UserAgent _userAgent;
	private readonly Func<HttpMethod, int, bool> _statusCodeToResponseSuccess;
	private TimeSpan _dnsRefreshTimeout;
	private bool _disableMetaHeader;
	private readonly MetaHeaderProvider? _metaHeaderProvider;
	private HeadersList? _responseHeadersToParse;
	private bool? _parseAllHeaders;
	private DateTimeProvider _dateTimeProvider;
	private RequestPipelineFactory _pipelineProvider;
	private List<IResponseBuilder>? _responseBuilders;

	SemaphoreSlim ITransportConfiguration.BootstrapLock => _bootstrapLock;
	IRequestInvoker ITransportConfiguration.RequestInvoker => _requestInvoker;
	int ITransportConfiguration.ConnectionLimit => _connectionLimit;
	NodePool ITransportConfiguration.NodePool => _nodePool;
	ProductRegistration ITransportConfiguration.ProductRegistration => _productRegistration;

	DateTimeProvider? ITransportConfiguration.DateTimeProvider => _dateTimeProvider;
	RequestPipelineFactory? ITransportConfiguration.PipelineProvider => _pipelineProvider;

	TimeSpan? ITransportConfiguration.DeadTimeout => _deadTimeout;
	bool ITransportConfiguration.DisableAutomaticProxyDetection => _disableAutomaticProxyDetection;
	TimeSpan? ITransportConfiguration.KeepAliveInterval => _keepAliveInterval;
	TimeSpan? ITransportConfiguration.KeepAliveTime => _keepAliveTime;
	TimeSpan? ITransportConfiguration.MaxDeadTimeout => _maxDeadTimeout;
	MemoryStreamFactory ITransportConfiguration.MemoryStreamFactory => _memoryStreamFactory;
	Func<Node, bool>? ITransportConfiguration.NodePredicate => _nodePredicate;
	Action<ApiCallDetails>? ITransportConfiguration.OnRequestCompleted => _onRequestCompleted;
	Action<RequestData>? ITransportConfiguration.OnRequestDataCreated => _onRequestDataCreated;
	string? ITransportConfiguration.ProxyAddress => _proxyAddress;
	string? ITransportConfiguration.ProxyPassword => _proxyPassword;
	string? ITransportConfiguration.ProxyUsername => _proxyUsername;
	NameValueCollection? ITransportConfiguration.QueryStringParameters => _queryStringParameters;
	Serializer ITransportConfiguration.RequestResponseSerializer => _requestResponseSerializer;
	Func<object, X509Certificate, X509Chain, SslPolicyErrors, bool>? ITransportConfiguration.ServerCertificateValidationCallback => _serverCertificateValidationCallback;
	string? ITransportConfiguration.CertificateFingerprint => _certificateFingerprint;
	IReadOnlyCollection<int>? ITransportConfiguration.SkipDeserializationForStatusCodes => _skipDeserializationForStatusCodes;
	TimeSpan? ITransportConfiguration.SniffInformationLifeSpan => _sniffInformationLifeSpan;
	bool ITransportConfiguration.SniffsOnConnectionFault => _sniffsOnConnectionFault;
	bool ITransportConfiguration.SniffsOnStartup => _sniffsOnStartup;
	UrlFormatter ITransportConfiguration.UrlFormatter => _urlFormatter;
	UserAgent ITransportConfiguration.UserAgent => _userAgent;
	Func<HttpMethod, int, bool> ITransportConfiguration.StatusCodeToResponseSuccess => _statusCodeToResponseSuccess;
	TimeSpan ITransportConfiguration.DnsRefreshTimeout => _dnsRefreshTimeout;
	bool ITransportConfiguration.PrettyJson => _prettyJson;
	IReadOnlyCollection<IResponseBuilder> ITransportConfiguration.ResponseBuilders => _responseBuilders ?? [];

	HeadersList? IRequestConfiguration.ResponseHeadersToParse => _responseHeadersToParse;
	string? IRequestConfiguration.RunAs => _runAs;
	bool? IRequestConfiguration.ThrowExceptions => _throwExceptions;
	bool? IRequestConfiguration.TransferEncodingChunked => _transferEncodingChunked;
	NameValueCollection? IRequestConfiguration.Headers => _headers;
	bool? IRequestConfiguration.EnableTcpStats => _enableTcpStats;
	bool? IRequestConfiguration.EnableThreadPoolStats => _enableThreadPoolStats;
	RequestMetaData? IRequestConfiguration.RequestMetaData => _requestMetaData;
	MetaHeaderProvider? ITransportConfiguration.MetaHeaderProvider => _metaHeaderProvider;
	bool ITransportConfiguration.DisableMetaHeader => _disableMetaHeader;
	string? IRequestConfiguration.Accept => _accept;
	IReadOnlyCollection<int>? IRequestConfiguration.AllowedStatusCodes => _allowedStatusCodes;
	AuthorizationHeader? IRequestConfiguration.Authentication => _authentication;
	X509CertificateCollection? IRequestConfiguration.ClientCertificates => _clientCertificates;
	string? IRequestConfiguration.ContentType => _contentType;
	bool? IRequestConfiguration.DisableDirectStreaming => _disableDirectStreaming;
	bool? IRequestConfiguration.DisableAuditTrail => _disableAuditTrail;
	bool? IRequestConfiguration.DisablePings => _disablePings;
	bool? IRequestConfiguration.DisableSniff => _disableSniff;
	bool? IRequestConfiguration.HttpPipeliningEnabled => _httpPipeliningEnabled;
	bool? IRequestConfiguration.EnableHttpCompression => _enableHttpCompression;
	Uri? IRequestConfiguration.ForceNode => _forceNode;
	int? IRequestConfiguration.MaxRetries => _maxRetries;
	TimeSpan? IRequestConfiguration.MaxRetryTimeout => _maxRetryTimeout;
	string? IRequestConfiguration.OpaqueId => _opaqueId;
	bool? IRequestConfiguration.ParseAllHeaders => _parseAllHeaders;
	TimeSpan? IRequestConfiguration.PingTimeout => _pingTimeout;
	TimeSpan? IRequestConfiguration.RequestTimeout => _requestTimeout;

	/// <summary>
	/// Allows more specialized implementations of <see cref="TransportConfigurationDescriptorBase{T}"/> to use their own
	/// request response serializer defaults
	/// </summary>
	// ReSharper disable once MemberCanBePrivate.Global
	// ReSharper disable once AutoPropertyCanBeMadeGetOnly.Global
	protected Serializer UseThisRequestResponseSerializer
	{
		get => _requestResponseSerializer;
		set => _requestResponseSerializer = value;
	}

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
		Assign(keepAliveTime, static (a, v) => a._keepAliveTime = v)
		.Assign(keepAliveInterval, static (a, v) => a._keepAliveInterval = v);

	/// <inheritdoc cref="IRequestConfiguration.MaxRetries"/>
	public T MaximumRetries(int maxRetries) => Assign(maxRetries, static (a, v) => a._maxRetries = v);

	/// <summary>
	/// <inheritdoc cref="ITransportConfiguration.ConnectionLimit" path="/summary"/>
	/// </summary>
	/// <param name="connectionLimit">The connection limit, a value lower then 0 will cause the connection limit not to be set at all</param>
	public T ConnectionLimit(int connectionLimit) => Assign(connectionLimit, static (a, v) => a._connectionLimit = v);

	/// <inheritdoc cref="ITransportConfiguration.SniffsOnConnectionFault"/>
	public T SniffOnConnectionFault(bool sniffsOnConnectionFault = true) =>
		Assign(sniffsOnConnectionFault, static (a, v) => a._sniffsOnConnectionFault = v);

	/// <inheritdoc cref="ITransportConfiguration.SniffsOnStartup"/>
	public T SniffOnStartup(bool sniffsOnStartup = true) => Assign(sniffsOnStartup, static (a, v) => a._sniffsOnStartup = v);

	/// <summary>
	/// <inheritdoc cref="ITransportConfiguration.SniffInformationLifeSpan" path="/summary"/>
	/// </summary>
	/// <param name="sniffLifeSpan">The duration a clusterstate is considered fresh, set to null to disable periodic sniffing</param>
	public T SniffLifeSpan(TimeSpan? sniffLifeSpan) => Assign(sniffLifeSpan, static (a, v) => a._sniffInformationLifeSpan = v);

	/// <inheritdoc cref="IRequestConfiguration.EnableHttpCompression"/>
	public T EnableHttpCompression(bool enabled = true) => Assign(enabled, static (a, v) => a._enableHttpCompression = v);

	/// <inheritdoc cref="ITransportConfiguration.DisableAutomaticProxyDetection"/>
	public T DisableAutomaticProxyDetection(bool disable = true) => Assign(disable, static (a, v) => a._disableAutomaticProxyDetection = v);

	/// <inheritdoc cref="IRequestConfiguration.ThrowExceptions"/>
	public T ThrowExceptions(bool alwaysThrow = true) => Assign(alwaysThrow, static (a, v) => a._throwExceptions = v);

	/// <inheritdoc cref="IRequestConfiguration.DisablePings"/>
	public T DisablePing(bool disable = true) => Assign(disable, static (a, v) => a._disablePings = v);

	/// <inheritdoc cref="ITransportConfiguration.QueryStringParameters"/>
	// ReSharper disable once MemberCanBePrivate.Global
	public T GlobalQueryStringParameters(NameValueCollection queryStringParameters) => Assign(queryStringParameters, static (a, v) =>
	{
		a._queryStringParameters ??= [];
		a._queryStringParameters.Add(v);
	});

	/// <inheritdoc cref="IRequestConfiguration.Headers"/>
	public T GlobalHeaders(NameValueCollection headers) => Assign(headers, static (a, v) =>
	{
		a._headers ??= [];
		a._headers.Add(v);
	});

	/// <inheritdoc cref="IRequestConfiguration.RequestTimeout"/>
	public T RequestTimeout(TimeSpan timeout) => Assign(timeout, static (a, v) => a._requestTimeout = v);

	/// <inheritdoc cref="IRequestConfiguration.PingTimeout"/>
	public T PingTimeout(TimeSpan timeout) => Assign(timeout, static (a, v) => a._pingTimeout = v);

	/// <inheritdoc cref="ITransportConfiguration.DeadTimeout"/>
	public T DeadTimeout(TimeSpan timeout) => Assign(timeout, static (a, v) => a._deadTimeout = v);

	/// <inheritdoc cref="ITransportConfiguration.MaxDeadTimeout"/>
	public T MaxDeadTimeout(TimeSpan timeout) => Assign(timeout, static (a, v) => a._maxDeadTimeout = v);

	/// <inheritdoc cref="IRequestConfiguration.MaxRetryTimeout"/>
	public T MaxRetryTimeout(TimeSpan maxRetryTimeout) => Assign(maxRetryTimeout, static (a, v) => a._maxRetryTimeout = v);

	/// <inheritdoc cref="ITransportConfiguration.DnsRefreshTimeout"/>
	public T DnsRefreshTimeout(TimeSpan timeout) => Assign(timeout, static (a, v) => a._dnsRefreshTimeout = v);

	/// <inheritdoc cref="ITransportConfiguration.CertificateFingerprint"/>
	public T CertificateFingerprint(string fingerprint) => Assign(fingerprint, static (a, v) => a._certificateFingerprint = v);

	/// <summary>
	/// If your connection has to go through proxy, use this method to specify the proxy url
	/// </summary>
	public T Proxy(Uri proxyAddress, string username, string password) =>
		Assign(proxyAddress.ToString(), static (a, v) => a._proxyAddress = v)
			.Assign(username, static (a, v) => a._proxyUsername = v)
			.Assign(password, static (a, v) => a._proxyPassword = v);

	/// <summary>
	/// If your connection has to go through proxy, use this method to specify the proxy url
	/// </summary>
	public T Proxy(Uri proxyAddress) =>
		Assign(proxyAddress.ToString(), static (a, v) => a._proxyAddress = v);

	/// <inheritdoc cref="IRequestConfiguration.DisableDirectStreaming"/>
	// ReSharper disable once MemberCanBePrivate.Global
	public T DisableDirectStreaming(bool b = true) => Assign(b, static (a, v) => a._disableDirectStreaming = v);

	/// <inheritdoc cref="IRequestConfiguration.DisableAuditTrail"/>
	public T DisableAuditTrail(bool b = true) => Assign(b, static (a, v) => a._disableAuditTrail = v);

	/// <inheritdoc cref="ITransportConfiguration.OnRequestCompleted"/>
	public T OnRequestCompleted(Action<ApiCallDetails> handler) =>
		Assign(handler, static (a, v) => a._onRequestCompleted += v ?? DefaultCompletedRequestHandler);

	/// <inheritdoc cref="ITransportConfiguration.OnRequestDataCreated"/>
	public T OnRequestDataCreated(Action<RequestData> handler) =>
		Assign(handler, static (a, v) => a._onRequestDataCreated += v ?? DefaultRequestDataCreated);

	/// <inheritdoc cref="AuthorizationHeader"/>
	public T Authentication(AuthorizationHeader header) => Assign(header, static (a, v) => a._authentication = v);

	/// <inheritdoc cref="IRequestConfiguration.HttpPipeliningEnabled"/>
	public T EnableHttpPipelining(bool enabled = true) => Assign(enabled, static (a, v) => a._httpPipeliningEnabled = v);

	/// <summary>
	/// <inheritdoc cref="ITransportConfiguration.NodePredicate"/>
	/// </summary>
	/// <param name="predicate">Return true if you want the node to be used for API calls</param>
	public T NodePredicate(Func<Node, bool> predicate) => Assign(predicate, static (a, v) => a._nodePredicate = v);

	/// <inheritdoc cref="ITransportConfiguration.ResponseBuilders"/>
	public T ResponseBuilder(IResponseBuilder responseBuilder) => Assign(responseBuilder, static (a, v) =>
	{
		a._responseBuilders ??= [];
		a._responseBuilders.Add(v);
	});

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
	public virtual T EnableDebugMode(Action<ApiCallDetails>? onRequestCompleted = null) =>
		PrettyJson()
			.DisableDirectStreaming()
			.EnableTcpStats()
			.EnableThreadPoolStats()
			.Assign(onRequestCompleted, static (a, v) =>
				a._onRequestCompleted += v ?? (d => Debug.WriteLine(d.DebugInformation)));

	/// <inheritdoc cref="ITransportConfiguration.PrettyJson"/>
	// ReSharper disable once VirtualMemberNeverOverridden.Global
	// ReSharper disable once MemberCanBeProtected.Global
	public virtual T PrettyJson(bool b = true) => Assign(b, static (a, v) => a._prettyJson = v);

	/// <inheritdoc cref="IRequestConfiguration.ParseAllHeaders"/>
	public virtual T ParseAllHeaders(bool b = true) => Assign(b, static (a, v) => a._parseAllHeaders = v);

	/// <inheritdoc cref="IRequestConfiguration.ResponseHeadersToParse"/>
	public virtual T ResponseHeadersToParse(HeadersList headersToParse) =>
		Assign(headersToParse, static (a, v) => a._responseHeadersToParse = new HeadersList(a._responseHeadersToParse, v));

	/// <inheritdoc cref="ITransportConfiguration.ServerCertificateValidationCallback"/>
	public T ServerCertificateValidationCallback(Func<object, X509Certificate, X509Chain, SslPolicyErrors, bool> callback) =>
		Assign(callback, static (a, v) => a._serverCertificateValidationCallback = v);

	/// <inheritdoc cref="IRequestConfiguration.ClientCertificates"/>
	public T ClientCertificates(X509CertificateCollection certificates) =>
		Assign(certificates, static (a, v) => a._clientCertificates = v);

	/// <inheritdoc cref="IRequestConfiguration.ClientCertificates"/>
	public T ClientCertificate(X509Certificate certificate) =>
		Assign(new X509Certificate2Collection { certificate }, static (a, v) => a._clientCertificates = v);

	/// <inheritdoc cref="IRequestConfiguration.ClientCertificates"/>
	public T ClientCertificate(string certificatePath) =>
		Assign(new X509Certificate2Collection { new X509Certificate(certificatePath) }, static (a, v) => a._clientCertificates = v);

	/// <inheritdoc cref="ITransportConfiguration.SkipDeserializationForStatusCodes"/>
	public T SkipDeserializationForStatusCodes(params int[] statusCodes) =>
		Assign(new ReadOnlyCollection<int>(statusCodes), static (a, v) => a._skipDeserializationForStatusCodes = v);

	/// <inheritdoc cref="ITransportConfiguration.UserAgent"/>
	public T UserAgent(UserAgent userAgent) => Assign(userAgent, static (a, v) => a._userAgent = v);

	/// <inheritdoc cref="IRequestConfiguration.TransferEncodingChunked"/>
	public T TransferEncodingChunked(bool transferEncodingChunked = true) => Assign(transferEncodingChunked, static (a, v) => a._transferEncodingChunked = v);

	/// <inheritdoc cref="ITransportConfiguration.MemoryStreamFactory"/>
	public T MemoryStreamFactory(MemoryStreamFactory memoryStreamFactory) => Assign(memoryStreamFactory, static (a, v) => a._memoryStreamFactory = v);

	/// <inheritdoc cref="ITransportConfiguration.PipelineProvider"/>>
	public T PipelineProvider(RequestPipelineFactory provider) => Assign(provider, static (a, v) => a._pipelineProvider = v);

	/// <inheritdoc cref="IRequestConfiguration.EnableTcpStats"/>>
	public T EnableTcpStats(bool enableTcpStats = true) => Assign(enableTcpStats, static (a, v) => a._enableTcpStats = v);

	/// <inheritdoc cref="IRequestConfiguration.EnableThreadPoolStats"/>>
	public T EnableThreadPoolStats(bool enableThreadPoolStats = true) => Assign(enableThreadPoolStats, static (a, v) => a._enableThreadPoolStats = v);

	/// <inheritdoc cref="ITransportConfiguration.DisableMetaHeader"/>>
	public T DisableMetaHeader(bool disable = true) => Assign(disable, static (a, v) => a._disableMetaHeader = v);

	// ReSharper disable once VirtualMemberNeverOverridden.Global
	/// <summary> Allows subclasses to hook into the parents dispose </summary>
	protected virtual void DisposeManagedResources()
	{
		_nodePool.Dispose();
		_requestInvoker?.Dispose();
		_bootstrapLock.Dispose();
	}

	/// <summary> Allows subclasses to add/remove default global query string parameters </summary>
	protected T UpdateGlobalQueryString(string key, string value, bool enabled)
	{
		_queryStringParameters ??= new();
		if (!enabled && _queryStringParameters[key] != null) _queryStringParameters.Remove(key);
		else if (enabled && _queryStringParameters[key] == null)
			return GlobalQueryStringParameters(new NameValueCollection { { key, "true" } });
		return (T)this;
	}

	void IDisposable.Dispose() => DisposeManagedResources();
}
