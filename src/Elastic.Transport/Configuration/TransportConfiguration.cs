// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net.Security;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using Elastic.Transport.Products;

namespace Elastic.Transport;

/// <summary>
/// Allows you to control how <see cref="ITransport{TConfiguration}"/> behaves and where/how it connects to Elastic Stack products
/// </summary>
public record TransportConfiguration : ITransportConfiguration
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
	public static MemoryStreamFactory DefaultMemoryStreamFactory { get; } = Transport.DefaultMemoryStreamFactory.Default;

	/// <summary>
	/// The default timeout before a TCP connection is forcefully recycled so that DNS updates come through
	/// Defaults to 5 minutes.
	/// </summary>
	public static readonly TimeSpan DefaultDnsRefreshTimeout = TimeSpan.FromMinutes(5);

#pragma warning disable 1587
#pragma warning disable 1570
	/// <summary>
	/// The default concurrent connection limit for outgoing http requests. Defaults to <c>80</c>
#if !NETFRAMEWORK	/// <para>Except for <see cref="HttpClientHandler"/> implementations based on curl, which defaults to <see cref="Environment.ProcessorCount"/></para>
#endif
	/// </summary>
#pragma warning restore 1570
#pragma warning restore 1587
	public static readonly int DefaultConnectionLimit = UsingCurlHandler ? Environment.ProcessorCount : 80;

	/// <summary>
	/// Creates a new instance of <see cref="TransportConfigurationDescriptor"/>
	/// </summary>
	/// <param name="uri">The root of the Elastic stack product node we want to connect to. Defaults to http://localhost:9200</param>
	/// <param name="productRegistration"><inheritdoc cref="ProductRegistration" path="/summary"/></param>
	public TransportConfiguration(Uri uri = null, ProductRegistration productRegistration = null)
		: this(new SingleNodePool(uri ?? new Uri("http://localhost:9200")), productRegistration: productRegistration) { }

	/// <summary>
	/// Sets up the client to communicate to Elastic Cloud using <paramref name="cloudId"/>,
	/// <para><see cref="CloudNodePool"/> documentation for more information on how to obtain your Cloud Id</para>
	/// </summary>
	public TransportConfiguration(string cloudId, BasicAuthentication credentials, ProductRegistration productRegistration = null)
		: this(new CloudNodePool(cloudId, credentials), productRegistration: productRegistration) { }

	/// <summary>
	/// Sets up the client to communicate to Elastic Cloud using <paramref name="cloudId"/>,
	/// <para><see cref="CloudNodePool"/> documentation for more information on how to obtain your Cloud Id</para>
	/// </summary>
	public TransportConfiguration(string cloudId, Base64ApiKey credentials, ProductRegistration productRegistration = null)
		: this(new CloudNodePool(cloudId, credentials), productRegistration: productRegistration) { }

	/// <summary> <inheritdoc cref="TransportConfigurationDescriptor" path="/summary"/></summary>
	/// <param name="nodePool"><inheritdoc cref="NodePool" path="/summary"/></param>
	/// <param name="invoker"><inheritdoc cref="IRequestInvoker" path="/summary"/></param>
	/// <param name="serializer"><inheritdoc cref="Serializer" path="/summary"/></param>
	/// <param name="productRegistration"><inheritdoc cref="ProductRegistration" path="/summary"/></param>
	public TransportConfiguration(
		NodePool nodePool,
		IRequestInvoker? invoker = null,
		Serializer? serializer = null,
		ProductRegistration? productRegistration = null
	)
	{
		//non init properties
		NodePool = nodePool;
		ProductRegistration = productRegistration ?? DefaultProductRegistration.Default;
		Connection = invoker ?? new HttpRequestInvoker();
		Accept = productRegistration?.DefaultMimeType;
		RequestResponseSerializer = serializer ?? new LowLevelRequestResponseSerializer();

		ConnectionLimit = DefaultConnectionLimit;
		DnsRefreshTimeout = DefaultDnsRefreshTimeout;
		MemoryStreamFactory = DefaultMemoryStreamFactory;
		SniffsOnConnectionFault = true;
		SniffsOnStartup = true;
		SniffInformationLifeSpan = TimeSpan.FromHours(1);

		MetaHeaderProvider = productRegistration?.MetaHeaderProvider;
		UrlFormatter = new UrlFormatter(this);
		StatusCodeToResponseSuccess = (m, i) => ProductRegistration.HttpStatusCodeClassifier(m, i);
		UserAgent = UserAgent.Create(ProductRegistration.Name, ProductRegistration.GetType());

		if (nodePool is CloudNodePool cloudPool)
		{
			Authentication = cloudPool.AuthenticationHeader;
			EnableHttpCompression = true;
		}

		ResponseHeadersToParse = new HeadersList(ProductRegistration.ResponseHeadersToParse);
	}

	/// Expert usage: Create a new transport configuration based of a previously configured instance
	public TransportConfiguration(ITransportConfiguration config)
	{
		if (config is null)
			throw new ArgumentNullException(nameof(config));

		Accept = config.Accept;
		AllowedStatusCodes = config.AllowedStatusCodes;
		Authentication = config.Authentication;
		BootstrapLock = config.BootstrapLock;
		CertificateFingerprint = config.CertificateFingerprint;
		ClientCertificates = config.ClientCertificates;
		Connection = config.Connection;
		ConnectionLimit = config.ConnectionLimit;
		ContentType = config.ContentType;
		DeadTimeout = config.DeadTimeout;
		DisableAuditTrail = config.DisableAuditTrail;
		DisableAutomaticProxyDetection = config.DisableAutomaticProxyDetection;
		DisableDirectStreaming = config.DisableDirectStreaming;
		DisableMetaHeader = config.DisableMetaHeader;
		DisablePings = config.DisablePings;
		DisableSniff = config.DisableSniff;
		DnsRefreshTimeout = config.DnsRefreshTimeout;
		EnableHttpCompression = config.EnableHttpCompression;
		EnableTcpStats = config.EnableTcpStats;
		EnableThreadPoolStats = config.EnableThreadPoolStats;
		ForceNode = config.ForceNode;
		Headers = config.Headers;
		HttpPipeliningEnabled = config.HttpPipeliningEnabled;
		KeepAliveInterval = config.KeepAliveInterval;
		KeepAliveTime = config.KeepAliveTime;
		MaxDeadTimeout = config.MaxDeadTimeout;
		MaxRetries = config.MaxRetries;
		MaxRetryTimeout = config.MaxRetryTimeout;
		MemoryStreamFactory = config.MemoryStreamFactory;
		NodePool = config.NodePool;
		NodePredicate = config.NodePredicate;
		OnRequestCompleted = config.OnRequestCompleted;
		OnRequestDataCreated = config.OnRequestDataCreated;
		OpaqueId = config.OpaqueId;
		ParseAllHeaders = config.ParseAllHeaders;
		PingTimeout = config.PingTimeout;
		PrettyJson = config.PrettyJson;
		ProductRegistration = config.ProductRegistration;
		ProxyAddress = config.ProxyAddress;
		ProxyPassword = config.ProxyPassword;
		ProxyUsername = config.ProxyUsername;
		QueryStringParameters = config.QueryStringParameters;
		RequestMetaData = config.RequestMetaData;
		RequestResponseSerializer = config.RequestResponseSerializer;
		RequestTimeout = config.RequestTimeout;
		ResponseHeadersToParse = config.ResponseHeadersToParse;
		RunAs = config.RunAs;
		ServerCertificateValidationCallback = config.ServerCertificateValidationCallback;
		SkipDeserializationForStatusCodes = config.SkipDeserializationForStatusCodes;
		SniffInformationLifeSpan = config.SniffInformationLifeSpan;
		SniffsOnConnectionFault = config.SniffsOnConnectionFault;
		SniffsOnStartup = config.SniffsOnStartup;
		StatusCodeToResponseSuccess = config.StatusCodeToResponseSuccess;
		ThrowExceptions = config.ThrowExceptions;
		TransferEncodingChunked = config.TransferEncodingChunked;
		UrlFormatter = config.UrlFormatter;
		UserAgent = config.UserAgent;
	}

	/// <summary>
	/// Turns on settings that aid in debugging like DisableDirectStreaming() and PrettyJson()
	/// so that the original request and response JSON can be inspected. It also always asks the server for the full stack trace on errors
	/// </summary>
	public virtual bool DebugMode
	{
		get => PrettyJson;
		init
		{
			PrettyJson = value;
			DisableDirectStreaming = value;
			EnableTcpStats = value;
			EnableThreadPoolStats = value;
		}
	}

	/// <inheritdoc />
	public NodePool NodePool { get; }
	/// <inheritdoc />
	public ProductRegistration ProductRegistration { get; }
	/// <inheritdoc />
	public SemaphoreSlim BootstrapLock { get; } = new(1, 1);
	/// <inheritdoc />
	public IRequestInvoker Connection { get; }
	/// <inheritdoc />
	public Serializer RequestResponseSerializer { get; }


	/// <inheritdoc />
	// ReSharper disable UnusedAutoPropertyAccessor.Global
	public string? Accept { get; }
	/// <inheritdoc />
	public IReadOnlyCollection<int>? AllowedStatusCodes { get; init; }
	/// <inheritdoc />
	public AuthorizationHeader? Authentication { get; init; }
	/// <inheritdoc />
	public X509CertificateCollection? ClientCertificates { get; init; }
	/// <inheritdoc />
	public string? ContentType { get; init; }
	/// <inheritdoc />
	public bool? DisableDirectStreaming { get; init; }
	/// <inheritdoc />
	public bool? DisableAuditTrail { get; init; }
	/// <inheritdoc />
	public bool? DisablePings { get; init; }
	/// <inheritdoc />
	public bool? DisableSniff { get; init; }
	/// <inheritdoc />
	public bool? HttpPipeliningEnabled { get; init; }
	/// <inheritdoc />
	public bool? EnableHttpCompression { get; init; }
	/// <inheritdoc />
	public Uri? ForceNode { get; init; }
	/// <inheritdoc />
	public int? MaxRetries { get; init; }
	/// <inheritdoc />
	public TimeSpan? MaxRetryTimeout { get; init; }
	/// <inheritdoc />
	public string? OpaqueId { get; init; }
	/// <inheritdoc />
	public bool? ParseAllHeaders { get; init; }
	/// <inheritdoc />
	public TimeSpan? PingTimeout { get; init; }
	/// <inheritdoc />
	public TimeSpan? RequestTimeout { get; init; }
	/// <inheritdoc />
	public HeadersList? ResponseHeadersToParse { get; init; }
	/// <inheritdoc />
	public string? RunAs { get; init; }
	/// <inheritdoc />
	public bool? ThrowExceptions { get; init; }
	/// <inheritdoc />
	public bool? TransferEncodingChunked { get; init; }
	/// <inheritdoc />
	public NameValueCollection? Headers { get; init; }
	/// <inheritdoc />
	public bool? EnableTcpStats { get; init; }
	/// <inheritdoc />
	public bool? EnableThreadPoolStats { get; init; }
	/// <inheritdoc />
	public RequestMetaData? RequestMetaData { get; init; }

	/// <inheritdoc cref="IDisposable.Dispose()"/>>
	public void Dispose()
	{
		NodePool.Dispose();
		Connection.Dispose();
		BootstrapLock.Dispose();
	}

	/// <inheritdoc />
	public int ConnectionLimit { get; init; }
	/// <inheritdoc />
	public TimeSpan? DeadTimeout { get; init; }
	/// <inheritdoc />
	public bool DisableAutomaticProxyDetection { get; init; }
	/// <inheritdoc />
	public TimeSpan? KeepAliveInterval { get; init; }
	/// <inheritdoc />
	public TimeSpan? KeepAliveTime { get; init; }
	/// <inheritdoc />
	public TimeSpan? MaxDeadTimeout { get; init; }
	/// <inheritdoc />
	public MemoryStreamFactory MemoryStreamFactory { get; init; }
	/// <inheritdoc />
	public Func<Node, bool>? NodePredicate { get; init; }
	/// <inheritdoc />
	public Action<ApiCallDetails>? OnRequestCompleted { get; init; }
	/// <inheritdoc />
	public Action<RequestData>? OnRequestDataCreated { get; init; }
	//TODO URI
	/// <inheritdoc />
	public string? ProxyAddress { get; init; }
	/// <inheritdoc />
	public string? ProxyPassword { get; init; }
	/// <inheritdoc />
	public string? ProxyUsername { get; init; }
	/// <inheritdoc />
	public NameValueCollection? QueryStringParameters { get; init; }
	/// <inheritdoc />
	public Func<object, X509Certificate, X509Chain, SslPolicyErrors, bool>? ServerCertificateValidationCallback { get; init; }
	/// <inheritdoc />
	public string? CertificateFingerprint { get; init; }
	/// <inheritdoc />
	public IReadOnlyCollection<int>? SkipDeserializationForStatusCodes { get; init; }
	/// <inheritdoc />
	public TimeSpan? SniffInformationLifeSpan { get; init; }
	/// <inheritdoc />
	public bool SniffsOnConnectionFault { get; init; }
	/// <inheritdoc />
	public bool SniffsOnStartup { get; init; }
	/// <inheritdoc />
	public UrlFormatter UrlFormatter { get; init; }
	/// <inheritdoc />
	public UserAgent UserAgent { get; init; }
	/// <inheritdoc />
	public Func<HttpMethod, int, bool> StatusCodeToResponseSuccess { get; init; }
	/// <inheritdoc />
	public TimeSpan DnsRefreshTimeout { get; init; }
	/// <inheritdoc />
	public bool PrettyJson { get; init; }
	/// <inheritdoc />
	public MetaHeaderProvider? MetaHeaderProvider { get; init; }
	/// <inheritdoc />
	public bool DisableMetaHeader { get; init; }
	// ReSharper restore UnusedAutoPropertyAccessor.Global
}

