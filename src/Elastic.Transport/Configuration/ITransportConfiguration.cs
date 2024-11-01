// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using Elastic.Transport.Products;

namespace Elastic.Transport;

/// <summary>
/// All the transport configuration that you as the user can use to steer the behavior of the <see cref="ITransport{TConfiguration}"/> and all the components such
/// as <see cref="IRequestInvoker"/> <see cref="NodePool"/> and <see cref="Serializer"/>.
/// </summary>
public interface ITransportConfiguration : IRequestConfiguration, IDisposable
{
	/// <summary> Provides a <see cref="SemaphoreSlim"/> to transport implementations that need to limit access to a resource</summary>
	SemaphoreSlim BootstrapLock { get; }

	/// <summary> The connection abstraction behind which all actual IO happens</summary>
	IRequestInvoker Connection { get; }

	/// <summary>
	/// Limits the number of concurrent connections that can be opened to an endpoint. Defaults to 80 (see
	/// <see cref="TransportConfiguration.DefaultConnectionLimit" />).
	/// <para>
	/// For Desktop CLR, this setting applies to the DefaultConnectionLimit property on the  ServicePointManager object when creating
	/// ServicePoint objects, affecting the default <see cref="IRequestInvoker" /> implementation.
	/// </para>
	/// <para>
	/// For Core CLR, this setting applies to the MaxConnectionsPerServer property on the HttpClientHandler instances used by the HttpClient
	/// inside the default <see cref="IRequestInvoker" /> implementation
	/// </para>
	/// </summary>
	int ConnectionLimit { get; }

	/// <summary> The connection pool to use when talking with Elasticsearch </summary>
	NodePool NodePool { get; }

	/// <summary>
	/// Returns information about the current product making use of the transport.
	/// </summary>
	ProductRegistration ProductRegistration { get; }

	/// Allows you to wrap calls to <see cref="DateTime.Now"/>, mainly for testing purposes to not have to rely
	/// on the wall clock
	DateTimeProvider DateTimeProvider { get; }

	/// In charge of create a new <see cref="RequestPipeline" />
	RequestPipelineFactory PipelineProvider { get; }

	/// <summary>
	/// The time to put dead nodes out of rotation (this will be multiplied by the number of times they've been dead)
	/// </summary>
	TimeSpan? DeadTimeout { get; }

	/// <summary>
	/// Disabled proxy detection on the webrequest, in some cases this may speed up the first connection
	/// your appdomain makes, in other cases it will actually increase the time for the first connection.
	/// No silver bullet! use with care!
	/// </summary>
	bool DisableAutomaticProxyDetection { get; }

	/// <summary>
	/// KeepAliveInterval - specifies the interval, in milliseconds, between
	/// when successive keep-alive packets are sent if no acknowledgement is
	/// received.
	/// </summary>
	TimeSpan? KeepAliveInterval { get; }

	/// <summary>
	/// KeepAliveTime - specifies the timeout, in milliseconds, with no
	/// activity until the first keep-alive packet is sent.
	/// </summary>
	TimeSpan? KeepAliveTime { get; }

	/// <summary>
	/// The maximum amount of time a node is allowed to marked dead
	/// </summary>
	TimeSpan? MaxDeadTimeout { get; }

	/// <summary> Provides a memory stream factory</summary>
	MemoryStreamFactory MemoryStreamFactory { get; }

	/// <summary>
	/// Register a predicate to select which nodes that you want to execute API calls on. Note that sniffing requests omit this predicate and
	/// always execute on all nodes.
	/// When using an <see cref="NodePool" /> implementation that supports reseeding of nodes, this will default to omitting master only
	/// node from regular API calls.
	/// When using static or single node connection pooling it is assumed the list of node you instantiate the client with should be taken
	/// verbatim.
	/// </summary>
	Func<Node, bool>? NodePredicate { get; }

	/// <summary>
	/// Allows you to register a callback every time an API call is returned
	/// </summary>
	Action<ApiCallDetails>? OnRequestCompleted { get; }

	/// <summary>
	/// An action to run when the <see cref="RequestData" /> for a request has been
	/// created.
	/// </summary>
	Action<RequestData>? OnRequestDataCreated { get; }

	/// <summary>
	/// When set will force all connections through this proxy
	/// </summary>
	string? ProxyAddress { get; }

	/// <summary>
	/// The password for the proxy, when configured
	/// </summary>
	string? ProxyPassword { get; }

	/// <summary>
	/// The username for the proxy, when configured
	/// </summary>
	string? ProxyUsername { get; }

	/// <summary>
	/// Append these query string parameters automatically to every request
	/// </summary>
	NameValueCollection? QueryStringParameters { get; }

	/// <summary>The serializer to use to serialize requests and deserialize responses</summary>
	Serializer RequestResponseSerializer { get; }

	/// <summary>
	/// Register a ServerCertificateValidationCallback per request
	/// </summary>
	Func<object, X509Certificate, X509Chain, SslPolicyErrors, bool>? ServerCertificateValidationCallback { get; }

	/// <summary>
	/// During development, the server certificate fingerprint may be provided. When present, it is used to validate the
	/// certificate sent by the server. The fingerprint is expected to be the hex string representing the SHA256 public key fingerprint.
	/// </summary>
	string? CertificateFingerprint { get; }

	/// <summary>
	/// Configure the client to skip deserialization of certain status codes e.g: you run Elasticsearch behind a proxy that returns an unexpected
	/// json format
	/// </summary>
	IReadOnlyCollection<int>? SkipDeserializationForStatusCodes { get; }

	/// <summary>
	/// Force a new sniff for the cluster when the cluster state information is older than
	/// the specified timespan
	/// </summary>
	TimeSpan? SniffInformationLifeSpan { get; }

	/// <summary>
	/// Force a new sniff for the cluster state every time a connection dies
	/// </summary>
	bool SniffsOnConnectionFault { get; }

	/// <summary>
	/// Sniff the cluster state immediately on startup
	/// </summary>
	bool SniffsOnStartup { get; }

	/// <summary>
	/// Access to <see cref="UrlFormatter"/> instance that is aware of this <see cref="ITransportConfiguration"/> instance
	/// </summary>
	UrlFormatter UrlFormatter { get; }

	/// <summary>
	/// The user agent string to send with requests. Useful for debugging purposes to understand client and framework
	/// versions that initiate requests to Elasticsearch
	/// </summary>
	UserAgent UserAgent { get; }

	/// <summary>
	/// Allow you to override the status code inspection that sets <see cref="ApiCallDetails.HasSuccessfulStatusCode"/>
	/// <para>
	/// Defaults to validating the statusCode is greater or equal to 200 and less than 300
	/// </para>
	/// <para>
	/// When the request is using <see cref="HttpMethod.HEAD"/> 404 is valid out of the box as well
	/// </para>
	/// <para></para>
	/// <para>NOTE: if a request specifies <see cref="IRequestConfiguration.AllowedStatusCodes"/> this takes precedence</para>
	/// </summary>
	Func<HttpMethod, int, bool> StatusCodeToResponseSuccess { get; }

	/// <summary>
	/// DnsRefreshTimeout for the connections. Defaults to 5 minutes.
	/// </summary>
	TimeSpan DnsRefreshTimeout { get; }

	/// <summary>
	/// Provide hints to serializer and products to produce pretty, non minified json.
	/// <para>Note: this is not a guarantee you will always get prettified json</para>
	/// </summary>
	bool PrettyJson { get; }

	/// <summary>
	/// Produces the client meta header for a request.
	/// </summary>
	MetaHeaderProvider? MetaHeaderProvider { get; }

	/// <summary>
	/// Disables the meta header which is included on all requests by default. This header contains lightweight information
	/// about the client and runtime.
	/// </summary>
	bool DisableMetaHeader { get; }
}
