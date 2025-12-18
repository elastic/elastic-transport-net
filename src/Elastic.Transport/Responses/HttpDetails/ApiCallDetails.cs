// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Net.NetworkInformation;
using System.Text;
using Elastic.Transport.Diagnostics;
using Elastic.Transport.Diagnostics.Auditing;
using Elastic.Transport.Extensions;

namespace Elastic.Transport;

/// <summary>
///
/// </summary>
public sealed class ApiCallDetails
{
	private string? _debugInformation;

	internal ApiCallDetails() { }

	/// <summary>
	/// Access to the collection of <see cref="Audit"/> events that occurred during the request.
	/// </summary>>
	public IReadOnlyCollection<Audit>? AuditTrail { get; internal set; }

	/// <summary>
	/// Statistics about the worker and I/O completion port threads at the time of the request.
	/// </summary>
	internal IReadOnlyDictionary<string, ThreadPoolStatistics>? ThreadPoolStats { get; init; }

	/// <summary>
	/// Statistics about the number of ports in various TCP states at the time of the request.
	/// </summary>
	internal IReadOnlyDictionary<TcpState, int>? TcpStats { get; init; }

	/// <summary>
	/// Information used to debug the request.
	/// </summary>
	public string DebugInformation
	{
		get
		{
			if (_debugInformation != null)
				return _debugInformation;

			var sb = new StringBuilder();
			sb.AppendLine(ToString());
			_debugInformation = ResponseStatics.DebugInformationBuilder(this, sb);

			return _debugInformation;
		}
	}

	/// <summary>
	/// The <see cref="HttpMethod"/> used in the request.
	/// </summary>
	public HttpMethod HttpMethod { get; internal set; }

	/// <summary>
	/// The <see cref="HttpStatusCode"/> of the response.
	/// </summary>
	public int? HttpStatusCode { get; internal set; }

	/// <summary>
	/// The <see cref="Exception"/> that occurred during the request, othwerwise <c>null</c>.
	/// </summary>
	public Exception? OriginalException { get; internal set; }

	/// <summary>
	/// The buffered request bytes when using <see cref="IRequestConfiguration.DisableDirectStreaming"/>
	/// otherwise, <c>null</c>.
	/// </summary>
	public byte[]? RequestBodyInBytes { get; internal set; }

	/// <summary>
	/// The buffered response bytes when using <see cref="IRequestConfiguration.DisableDirectStreaming"/>
	/// otherwise, <c>null</c>.
	/// </summary>
	public byte[]? ResponseBodyInBytes { get; internal set; }

	/// <summary>
	/// The value of the Content-Type header in the response.
	/// </summary>
	[Obsolete("This property has been retired and replaced by ResponseContentType. " +
		"Prefer using the updated property as this will be removed in a future release.")]
	public string ResponseMimeType
	{
		get => ResponseContentType;
		set => ResponseContentType = value;
	}

	/// <summary>
	/// The value of the Content-Type header in the response.
	/// </summary>
	public string ResponseContentType { get; set; } = string.Empty;

	/// <summary>
	/// Indicates whether the response has a status code that is considered successful.
	/// </summary>
	public bool HasSuccessfulStatusCode { get; internal set; }

	/// <summary>
	/// Indicates whether the response has a Content-Type header that is expected.
	/// </summary>
	public bool HasExpectedContentType { get; internal set; }

	internal bool HasSuccessfulStatusCodeAndExpectedContentType => HasSuccessfulStatusCode && HasExpectedContentType;

	internal bool SuccessOrKnownError =>
		HasSuccessfulStatusCodeAndExpectedContentType
			|| HttpStatusCode >= 400
				&& HttpStatusCode < 599
				&& HttpStatusCode != 504 //Gateway timeout needs to be retried
				&& HttpStatusCode != 503 //service unavailable needs to be retried
				&& HttpStatusCode != 502
				&& HasExpectedContentType;

	/// <summary>
	/// The <see cref="Uri"/> of the request.
	/// </summary>
	public Uri? Uri { get; internal set; }

	internal ITransportConfiguration TransportConfiguration { get; set; } = null!; // Always set during initialization in ResponseFactory

	internal IReadOnlyDictionary<string, IEnumerable<string>> ParsedHeaders { get; set; }
		= EmptyReadOnly<string, IEnumerable<string>>.Dictionary;

	/// <summary>
	/// Tries to get the value of a header if present in the parsed headers.
	/// </summary>
	/// <param name="key">The name of the header to locate.</param>
	/// <param name="headerValues"> When this method returns, the value associated with the specified key, if the
	/// key is found; otherwise, the default value for the type of the value parameter. This parameter is passed uninitialized.</param>
	/// <returns>A <see cref="bool"/> indiciating whether the header was located.</returns>
	public bool TryGetHeader(string key, [NotNullWhen(true)] out IEnumerable<string>? headerValues) =>
		ParsedHeaders.TryGetValue(key, out headerValues);

	/// <summary>
	/// A string summarising the API call.
	/// </summary>
	public override string ToString()
	{
		var sb = new StringBuilder();
		sb.Append($"{(HasSuccessfulStatusCodeAndExpectedContentType ? "S" : "Uns")}uccessful ({HttpStatusCode}) low level call on ");
		sb.AppendLine($"{HttpMethod.GetStringValue()}: {(Uri is not null ? Uri.PathAndQuery : "UNKNOWN URI")}");
		if (OriginalException is not null)
			sb.AppendLine($" Exception: {OriginalException.Message}");
		return sb.ToString();
	}
}
