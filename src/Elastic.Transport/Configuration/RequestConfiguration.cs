// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Security;
using System.Security.Cryptography.X509Certificates;
using Elastic.Transport.Extensions;

namespace Elastic.Transport
{
	/// <summary>
	/// Allows you to inject per <see cref="ITransport.Request{TResponse}"/> overrides to the current <see cref="ITransportConfigurationValues"/>.
	/// </summary>
	public interface IRequestConfiguration
	{
		/// <summary>
		/// Force a different Accept header on the request
		/// </summary>
		string Accept { get; set; }

		/// <summary>
		/// Treat the following statuses (on top of the 200 range) NOT as error.
		/// </summary>
		IReadOnlyCollection<int> AllowedStatusCodes { get; set; }

		/// <summary>
		/// Provide an authentication header override for this request
		/// </summary>
		IAuthenticationHeader AuthenticationHeader { get; set; }

		/// <summary>
		/// Use the following client certificates to authenticate this single request
		/// </summary>
		X509CertificateCollection ClientCertificates { get; set; }

		/// <summary>
		/// Force a different Content-Type header on the request
		/// </summary>
		string ContentType { get; set; }

		/// <summary>
		/// Whether to buffer the request and response bytes for the call
		/// </summary>
		bool? DisableDirectStreaming { get; set; }

		/// <summary>
		/// Under no circumstance do a ping before the actual call. If a node was previously dead a small ping with
		/// low connect timeout will be tried first in normal circumstances
		/// </summary>
		bool? DisablePing { get; set; }

		/// <summary>
		/// Forces no sniffing to occur on the request no matter what configuration is in place
		/// globally
		/// </summary>
		bool? DisableSniff { get; set; }

		/// <summary>
		/// Whether or not this request should be pipelined. http://en.wikipedia.org/wiki/HTTP_pipelining defaults to true
		/// </summary>
		bool? EnableHttpPipelining { get; set; }

		/// <summary>
		/// This will force the operation on the specified node, this will bypass any configured connection pool and will no retry.
		/// </summary>
		Uri ForceNode { get; set; }

		/// <summary>
		/// This will override whatever is set on the connection configuration or whatever default the connectionpool has.
		/// </summary>
		int? MaxRetries { get; set; }

		/// <summary>
		/// Associate an Id with this user-initiated task, such that it can be located in the cluster task list.
		/// Valid only for Elasticsearch 6.2.0+
		/// </summary>
		string OpaqueId { get; set; }

		/// <summary>
		/// The ping timeout for this specific request
		/// </summary>
		TimeSpan? PingTimeout { get; set; }

		/// <summary>
		/// The timeout for this specific request, takes precedence over the global timeout settings
		/// </summary>
		TimeSpan? RequestTimeout { get; set; }

		/// <summary>
		/// Submit the request on behalf in the context of a different shield user
		/// <pre />https://www.elastic.co/guide/en/shield/current/submitting-requests-for-other-users.html
		/// </summary>
		string RunAs { get; set; }

		/// <summary>
		/// Instead of following a c/go like error checking on response.IsValid do throw an exception (except when <see cref="IApiCallDetails.SuccessOrKnownError"/> is false)
		/// on the client when a call resulted in an exception on either the client or the Elasticsearch server.
		/// <para>Reasons for such exceptions could be search parser errors, index missing exceptions, etc...</para>
		/// </summary>
		bool? ThrowExceptions { get; set; }

		/// <summary>
		/// Whether the request should be sent with chunked Transfer-Encoding.
		/// </summary>
		bool? TransferEncodingChunked { get; set; }

		/// <summary>
		/// Try to send these headers for this single request
		/// </summary>
		NameValueCollection Headers { get; set; }

		/// <inheritdoc cref="ITransportConfigurationValues.EnableTcpStats"/>
		bool? EnableTcpStats { get; set; }

		/// <inheritdoc cref="ITransportConfigurationValues.EnableThreadPoolStats"/>
		bool? EnableThreadPoolStats { get; set; }
	}

	/// <inheritdoc cref="IRequestConfiguration"/>
	public class RequestConfiguration : IRequestConfiguration
	{
		/// <inheritdoc />
		public string Accept { get; set; }
		/// <inheritdoc />
		public IReadOnlyCollection<int> AllowedStatusCodes { get; set; }
		/// <inheritdoc />
		public IAuthenticationHeader AuthenticationHeader { get; set; }
		/// <inheritdoc />
		public X509CertificateCollection ClientCertificates { get; set; }
		/// <inheritdoc />
		public string ContentType { get; set; }
		/// <inheritdoc />
		public bool? DisableDirectStreaming { get; set; }
		/// <inheritdoc />
		public bool? DisablePing { get; set; }
		/// <inheritdoc />
		public bool? DisableSniff { get; set; }
		/// <inheritdoc />
		public bool? EnableHttpPipelining { get; set; } = true;
		/// <inheritdoc />
		public Uri ForceNode { get; set; }
		/// <inheritdoc />
		public int? MaxRetries { get; set; }
		/// <inheritdoc />
		public string OpaqueId { get; set; }
		/// <inheritdoc />
		public TimeSpan? PingTimeout { get; set; }
		/// <inheritdoc />
		public TimeSpan? RequestTimeout { get; set; }
		/// <inheritdoc />
		public string RunAs { get; set; }
		/// <inheritdoc />
		public bool? ThrowExceptions { get; set; }
		/// <inheritdoc />
		public bool? TransferEncodingChunked { get; set; }
		/// <inheritdoc />
		public NameValueCollection Headers { get; set; }
		/// <inheritdoc />
		public bool? EnableTcpStats { get; set; }
		/// <inheritdoc />
		public bool? EnableThreadPoolStats { get; set; }
	}

	/// <inheritdoc cref="IRequestConfiguration"/>
	public class RequestConfigurationDescriptor : IRequestConfiguration
	{
		/// <inheritdoc cref="IRequestConfiguration"/>
		public RequestConfigurationDescriptor(IRequestConfiguration config)
		{
			Self.RequestTimeout = config?.RequestTimeout;
			Self.PingTimeout = config?.PingTimeout;
			Self.ContentType = config?.ContentType;
			Self.Accept = config?.Accept;
			Self.MaxRetries = config?.MaxRetries;
			Self.ForceNode = config?.ForceNode;
			Self.DisableSniff = config?.DisableSniff;
			Self.DisablePing = config?.DisablePing;
			Self.DisableDirectStreaming = config?.DisableDirectStreaming;
			Self.AllowedStatusCodes = config?.AllowedStatusCodes;
			Self.AuthenticationHeader = config?.AuthenticationHeader;
			Self.EnableHttpPipelining = config?.EnableHttpPipelining ?? true;
			Self.RunAs = config?.RunAs;
			Self.ClientCertificates = config?.ClientCertificates;
			Self.ThrowExceptions = config?.ThrowExceptions;
			Self.OpaqueId = config?.OpaqueId;
			Self.TransferEncodingChunked = config?.TransferEncodingChunked;
			Self.Headers = config?.Headers;
			Self.EnableTcpStats = config?.EnableTcpStats;
			Self.EnableThreadPoolStats = config?.EnableThreadPoolStats;
		}

		string IRequestConfiguration.Accept { get; set; }
		IReadOnlyCollection<int> IRequestConfiguration.AllowedStatusCodes { get; set; }
		IAuthenticationHeader IRequestConfiguration.AuthenticationHeader { get; set; }
		X509CertificateCollection IRequestConfiguration.ClientCertificates { get; set; }
		string IRequestConfiguration.ContentType { get; set; }
		bool? IRequestConfiguration.DisableDirectStreaming { get; set; }
		bool? IRequestConfiguration.DisablePing { get; set; }
		bool? IRequestConfiguration.DisableSniff { get; set; }
		bool? IRequestConfiguration.EnableHttpPipelining { get; set; } = true;
		Uri IRequestConfiguration.ForceNode { get; set; }
		int? IRequestConfiguration.MaxRetries { get; set; }
		string IRequestConfiguration.OpaqueId { get; set; }
		TimeSpan? IRequestConfiguration.PingTimeout { get; set; }
		TimeSpan? IRequestConfiguration.RequestTimeout { get; set; }
		string IRequestConfiguration.RunAs { get; set; }
		private IRequestConfiguration Self => this;
		bool? IRequestConfiguration.ThrowExceptions { get; set; }
		bool? IRequestConfiguration.TransferEncodingChunked { get; set; }
		NameValueCollection IRequestConfiguration.Headers { get; set; }
		bool? IRequestConfiguration.EnableTcpStats { get; set; }
		bool? IRequestConfiguration.EnableThreadPoolStats { get; set; }

		/// <inheritdoc cref="IRequestConfiguration.RunAs"/>
		public RequestConfigurationDescriptor RunAs(string username)
		{
			Self.RunAs = username;
			return this;
		}

		/// <inheritdoc cref="IRequestConfiguration.RequestTimeout"/>
		public RequestConfigurationDescriptor RequestTimeout(TimeSpan requestTimeout)
		{
			Self.RequestTimeout = requestTimeout;
			return this;
		}

		/// <inheritdoc cref="IRequestConfiguration.OpaqueId"/>
		public RequestConfigurationDescriptor OpaqueId(string opaqueId)
		{
			Self.OpaqueId = opaqueId;
			return this;
		}

		/// <inheritdoc cref="IRequestConfiguration.PingTimeout"/>
		public RequestConfigurationDescriptor PingTimeout(TimeSpan pingTimeout)
		{
			Self.PingTimeout = pingTimeout;
			return this;
		}

		/// <inheritdoc cref="IRequestConfiguration.ContentType"/>
		public RequestConfigurationDescriptor ContentType(string contentTypeHeader)
		{
			Self.ContentType = contentTypeHeader;
			return this;
		}

		/// <inheritdoc cref="IRequestConfiguration.Accept"/>
		public RequestConfigurationDescriptor Accept(string acceptHeader)
		{
			Self.Accept = acceptHeader;
			return this;
		}

		/// <inheritdoc cref="IRequestConfiguration.AllowedStatusCodes"/>
		public RequestConfigurationDescriptor AllowedStatusCodes(IEnumerable<int> codes)
		{
			Self.AllowedStatusCodes = codes?.ToReadOnlyCollection();
			return this;
		}

		/// <inheritdoc cref="IRequestConfiguration.AllowedStatusCodes"/>
		public RequestConfigurationDescriptor AllowedStatusCodes(params int[] codes)
		{
			Self.AllowedStatusCodes = codes?.ToReadOnlyCollection();
			return this;
		}

		/// <inheritdoc cref="IRequestConfiguration.DisableSniff"/>
		public RequestConfigurationDescriptor DisableSniffing(bool? disable = true)
		{
			Self.DisableSniff = disable;
			return this;
		}

		/// <inheritdoc cref="IRequestConfiguration.DisablePing"/>
		public RequestConfigurationDescriptor DisablePing(bool? disable = true)
		{
			Self.DisablePing = disable;
			return this;
		}

		/// <inheritdoc cref="IRequestConfiguration.ThrowExceptions"/>
		public RequestConfigurationDescriptor ThrowExceptions(bool throwExceptions = true)
		{
			Self.ThrowExceptions = throwExceptions;
			return this;
		}

		/// <inheritdoc cref="IRequestConfiguration.DisableDirectStreaming"/>
		public RequestConfigurationDescriptor DisableDirectStreaming(bool? disable = true)
		{
			Self.DisableDirectStreaming = disable;
			return this;
		}

		/// <inheritdoc cref="IRequestConfiguration.ForceNode"/>
		public RequestConfigurationDescriptor ForceNode(Uri uri)
		{
			Self.ForceNode = uri;
			return this;
		}

		/// <inheritdoc cref="IRequestConfiguration.MaxRetries"/>
		public RequestConfigurationDescriptor MaxRetries(int retry)
		{
			Self.MaxRetries = retry;
			return this;
		}

		/// <inheritdoc cref="IAuthenticationHeader"/>
		public RequestConfigurationDescriptor Authentication(IAuthenticationHeader authentication)
		{
			Self.AuthenticationHeader = authentication;
			return this;
		}

		/// <inheritdoc cref="IRequestConfiguration.EnableHttpPipelining"/>
		public RequestConfigurationDescriptor EnableHttpPipelining(bool enable = true)
		{
			Self.EnableHttpPipelining = enable;
			return this;
		}

		/// <inheritdoc cref="IRequestConfiguration.ClientCertificates"/>
		public RequestConfigurationDescriptor ClientCertificates(X509CertificateCollection certificates)
		{
			Self.ClientCertificates = certificates;
			return this;
		}

		/// <inheritdoc cref="IRequestConfiguration.ClientCertificates"/>
		public RequestConfigurationDescriptor ClientCertificate(X509Certificate certificate) =>
			ClientCertificates(new X509Certificate2Collection { certificate });

		/// <inheritdoc cref="IRequestConfiguration.ClientCertificates"/>
		public RequestConfigurationDescriptor ClientCertificate(string certificatePath) =>
			ClientCertificates(new X509Certificate2Collection { new X509Certificate(certificatePath) });

		/// <inheritdoc cref="IRequestConfiguration.TransferEncodingChunked" />
		public RequestConfigurationDescriptor TransferEncodingChunked(bool? transferEncodingChunked = true)
		{
			Self.TransferEncodingChunked = transferEncodingChunked;
			return this;
		}

		/// <inheritdoc cref="IRequestConfiguration.Headers" />
		public RequestConfigurationDescriptor GlobalHeaders(NameValueCollection headers)
		{
			Self.Headers = headers;
			return this;
		}

		/// <inheritdoc cref="IRequestConfiguration.EnableTcpStats" />
		public RequestConfigurationDescriptor EnableTcpStats(bool? enableTcpStats = true)
		{
			Self.EnableTcpStats = enableTcpStats;
			return this;
		}

		/// <inheritdoc cref="IRequestConfiguration.EnableThreadPoolStats" />
		public RequestConfigurationDescriptor EnableThreadPoolStats(bool? enableThreadPoolStats = true)
		{
			Self.EnableThreadPoolStats = enableThreadPoolStats;
			return this;
		}
	}
}
