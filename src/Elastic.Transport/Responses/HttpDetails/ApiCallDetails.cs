// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Text;
using Elastic.Transport.Diagnostics;
using Elastic.Transport.Diagnostics.Auditing;
using Elastic.Transport.Extensions;

namespace Elastic.Transport
{
	/// <summary>
	/// 
	/// </summary>
	public sealed class ApiCallDetails
	{
		private string _debugInformation;

		internal ApiCallDetails() { }

		/// <summary>
		/// 
		/// </summary>>
		public IEnumerable<Audit> AuditTrail { get; internal set; }

		/// <summary>
		/// 
		/// </summary>
		internal IReadOnlyDictionary<string, ThreadPoolStatistics> ThreadPoolStats { get; set; }

		/// <summary>
		/// 
		/// </summary>
		internal IReadOnlyDictionary<TcpState, int> TcpStats { get; set; }

		/// <summary>
		/// 
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
		/// 
		/// </summary>
		public HttpMethod HttpMethod { get; internal set; }

		/// <summary>
		/// 
		/// </summary>
		public int? HttpStatusCode { get; internal set; }

		/// <summary>
		/// 
		/// </summary>
		public Exception OriginalException { get; internal set; }

		/// <summary>
		/// 
		/// </summary>
		public byte[] RequestBodyInBytes { get; internal set; }

		/// <summary>
		/// 
		/// </summary>
		public byte[] ResponseBodyInBytes { get; internal set; }

		/// <summary>
		/// 
		/// </summary>
		internal string ResponseMimeType { get; set; }

		/// <summary>
		/// 
		/// </summary>
		public bool Success { get; internal set; }

		/// <summary>
		/// 
		/// </summary>
		internal bool SuccessOrKnownError =>
			Success || HttpStatusCode >= 400
			&& HttpStatusCode < 599
			&& HttpStatusCode != 504 //Gateway timeout needs to be retried
			&& HttpStatusCode != 503 //service unavailable needs to be retried
			&& HttpStatusCode != 502;

		/// <summary>
		/// 
		/// </summary>
		public Uri Uri { get; internal set; }

		/// <summary>
		/// 
		/// </summary>
		internal ITransportConfiguration TransportConfiguration { get; set; }

		/// <summary>
		/// 
		/// </summary>
		internal IReadOnlyDictionary<string, IEnumerable<string>> ParsedHeaders { get; set; }

		/// <summary>
		/// 
		/// </summary>
		/// <param name="key"></param>
		/// <param name="headerValues"></param>
		/// <returns></returns>
		// TODO: Nullable annotations
		public bool TryGetHeader(string key, out IEnumerable<string> headerValues)
			=> ParsedHeaders.TryGetValue(key, out headerValues);

		/// <summary>
		/// The error response if the server returned JSON describing a server error.
		/// </summary>
		internal ErrorResponse ErrorResponse { get; set; } = EmptyError.Instance;

		/// <summary>
		/// A string summarising the API call.
		/// </summary>
		public override string ToString() =>
			$"{(Success ? "S" : "Uns")}uccessful ({HttpStatusCode}) low level call on {HttpMethod.GetStringValue()}: {(Uri is not null ? Uri.PathAndQuery: "UNKNOWN URI")}";
	}
}
