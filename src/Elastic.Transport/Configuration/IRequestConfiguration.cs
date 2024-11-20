// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Security.Cryptography.X509Certificates;

namespace Elastic.Transport;

/// <summary>
/// Allows you to inject per request overrides to the current <see cref="ITransportConfiguration"/>.
/// </summary>
public interface IRequestConfiguration
{
	/// <summary>
	/// Force a different Accept header on the request
	/// </summary>
	string? Accept { get; }

	/// <summary>
	/// Treat the following statuses (on top of the 200 range) NOT as error.
	/// </summary>
	IReadOnlyCollection<int>? AllowedStatusCodes { get; }

	/// <summary> Provide an authentication header override for this request </summary>
	AuthorizationHeader? Authentication { get; }

	/// <summary>
	/// Use the following client certificates to authenticate this single request
	/// </summary>
	X509CertificateCollection? ClientCertificates { get; }

	/// <summary>
	/// Force a different Content-Type header on the request
	/// </summary>
	string? ContentType { get; }

	/// <summary>
	/// Whether to buffer the request and response bytes for the call
	/// </summary>
	bool? DisableDirectStreaming { get; }

	/// <summary>
	/// Whether to disable the audit trail for the request.
	/// </summary>
	bool? DisableAuditTrail { get; }

	/// <summary>
	/// Under no circumstance do a ping before the actual call. If a node was previously dead a small ping with
	/// low connect timeout will be tried first in normal circumstances
	/// </summary>
	bool? DisablePings { get; }

	/// <summary>
	/// Forces no sniffing to occur on the request no matter what configuration is in place
	/// globally
	/// </summary>
	bool? DisableSniff { get; }

	/// <summary>
	/// Whether or not this request should be pipelined. http://en.wikipedia.org/wiki/HTTP_pipelining defaults to true
	/// </summary>
	bool? HttpPipeliningEnabled { get; }

	/// <summary>
	/// Enable gzip compressed requests and responses
	/// </summary>
	bool? EnableHttpCompression { get; }

	/// <summary>
	/// This will force the operation on the specified node, this will bypass any configured connection pool and will no retry.
	/// </summary>
	Uri? ForceNode { get; }

	/// <summary>
	/// When a retryable exception occurs or status code is returned this controls the maximum
	/// amount of times we should retry the call to Elasticsearch
	/// </summary>
	int? MaxRetries { get; }

	/// <summary>
	/// Limits the total runtime including retries separately from <see cref="IRequestConfiguration.RequestTimeout" />
	/// <pre>
	/// When not specified defaults to <see cref="IRequestConfiguration.RequestTimeout" /> which itself defaults to 60 seconds
	/// </pre>
	/// </summary>
	TimeSpan? MaxRetryTimeout { get; }

	/// <summary>
	/// Associate an Id with this user-initiated task, such that it can be located in the cluster task list.
	/// Valid only for Elasticsearch 6.2.0+
	/// </summary>
	string? OpaqueId { get; }

	/// <summary> Determines whether to parse all HTTP headers in the request. </summary>
	bool? ParseAllHeaders { get; }

	/// <summary>
	/// The ping timeout for this specific request
	/// </summary>
	TimeSpan? PingTimeout { get; }

	/// <summary>
	/// The timeout for this specific request, takes precedence over the global timeout init
	/// </summary>
	TimeSpan? RequestTimeout { get; }

	/// <summary>
	/// Additional response builders to apply.
	/// </summary>
	IReadOnlyCollection<IResponseBuilder> ResponseBuilders { get; }

	/// <summary> Specifies the headers from the response that should be parsed. </summary>
	HeadersList? ResponseHeadersToParse { get; }

	/// <summary>
	/// Submit the request on behalf in the context of a different shield user
	/// <pre />https://www.elastic.co/guide/en/shield/current/submitting-requests-for-other-users.html
	/// </summary>
	string? RunAs { get; }

	/// <summary>
	/// Instead of following a c/go like error checking on response.IsValid do throw an exception (except when <see cref="ApiCallDetails.SuccessOrKnownError"/> is false)
	/// on the client when a call resulted in an exception on either the client or the Elasticsearch server.
	/// <para>Reasons for such exceptions could be search parser errors, index missing exceptions, etc...</para>
	/// </summary>
	bool? ThrowExceptions { get; }

	/// <summary>
	/// Whether the request should be sent with chunked Transfer-Encoding.
	/// </summary>
	bool? TransferEncodingChunked { get; }

	/// <summary>
	/// Try to send these headers for this single request
	/// </summary>
	NameValueCollection? Headers { get; }

	/// <summary>
	/// Enable statistics about TCP connections to be collected when making a request
	/// </summary>
	bool? EnableTcpStats { get; }

	/// <summary>
	/// Enable statistics about thread pools to be collected when making a request
	/// </summary>
	bool? EnableThreadPoolStats { get; }

	/// <summary>
	/// Holds additional meta data about the request.
	/// </summary>
	RequestMetaData? RequestMetaData { get; }

	/// <summary>
	/// The user agent string to send with requests. Useful for debugging purposes to understand client and framework
	/// versions that initiate requests to Elasticsearch
	/// </summary>
	UserAgent? UserAgent { get; }
}
