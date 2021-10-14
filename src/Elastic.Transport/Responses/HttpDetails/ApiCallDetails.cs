// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Net.NetworkInformation;
using System.Text;
using Elastic.Transport.Diagnostics;
using Elastic.Transport.Diagnostics.Auditing;
using Elastic.Transport.Extensions;

namespace Elastic.Transport
{
	/// <inheritdoc cref="IApiCallDetails"/>
	public class ApiCallDetails : IApiCallDetails
	{
		private string _debugInformation;

		/// <inheritdoc cref="IApiCallDetails.AuditTrail"/>
		public List<Audit> AuditTrail { get; set; }

		/// <inheritdoc cref="IApiCallDetails.ThreadPoolStats"/>
		public ReadOnlyDictionary<string, ThreadPoolStatistics> ThreadPoolStats { get; set; }

		/// <inheritdoc cref="IApiCallDetails.TcpStats"/>
		public ReadOnlyDictionary<TcpState, int> TcpStats { get; set; }

		/// <inheritdoc cref="IApiCallDetails.DebugInformation"/>
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

		/// <inheritdoc cref="IApiCallDetails.HttpMethod"/>
		public HttpMethod HttpMethod { get; set; }

		/// <inheritdoc cref="IApiCallDetails.HttpStatusCode"/>
		public int? HttpStatusCode { get; set; }

		/// <inheritdoc cref="IApiCallDetails.OriginalException"/>
		public Exception OriginalException { get; set; }

		/// <inheritdoc cref="IApiCallDetails.RequestBodyInBytes"/>
		public byte[] RequestBodyInBytes { get; set; }

		/// <inheritdoc cref="IApiCallDetails.RequestBodyInBytes"/>
		public byte[] ResponseBodyInBytes { get; set; }

		/// <inheritdoc cref="IApiCallDetails.ResponseMimeType"/>
		public string ResponseMimeType { get; set; }

		/// <inheritdoc cref="IApiCallDetails.Success"/>
		public bool Success { get; set; }

		/// <inheritdoc cref="IApiCallDetails.DebugInformation"/>
		public bool SuccessOrKnownError =>
			Success || HttpStatusCode >= 400
			&& HttpStatusCode < 599
			&& HttpStatusCode != 504 //Gateway timeout needs to be retried
			&& HttpStatusCode != 503 //service unavailable needs to be retried
			&& HttpStatusCode != 502;

		/// <inheritdoc cref="IApiCallDetails.DebugInformation"/>
		public Uri Uri { get; set; }

		/// <inheritdoc cref="IApiCallDetails.DebugInformation"/>
		public ITransportConfiguration ConnectionConfiguration { get; set; }

		/// <inheritdoc cref="IApiCallDetails.ParsedHeaders"/>
		public ReadOnlyDictionary<string, IEnumerable<string>> ParsedHeaders { get; set; }

		/// <inheritdoc cref="IApiCallDetails.DebugInformation"/>
		public override string ToString() =>
			$"{(Success ? "S" : "Uns")}uccessful ({HttpStatusCode}) low level call on {HttpMethod.GetStringValue()}: {Uri.PathAndQuery}";
	}
}
