// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Elastic.Transport.Diagnostics.Auditing;
using Elastic.Transport.Extensions;

namespace Elastic.Transport
{
	//TODO Put in diagnostics folder/namespace
	/// <summary>
	/// Creates human readable debug strings based on <see cref="IApiCallDetails"/> so that
	/// its clear what exactly transpired during a call into <see cref="ITransport.Request{TResponse}"/>
	/// </summary>
	public static class ResponseStatics
	{
		private static readonly string RequestAlreadyCaptured =
			"<Request stream not captured or already read to completion by serializer. Set DisableDirectStreaming() on ConnectionSettings to force it to be set on the response.>";

		private static readonly string ResponseAlreadyCaptured =
			"<Response stream not captured or already read to completion by serializer. Set DisableDirectStreaming() on ConnectionSettings to force it to be set on the response.>";

		/// <inheritdoc cref="ResponseStatics"/>
		public static string DebugInformationBuilder(IApiCallDetails r, StringBuilder sb)
		{
			sb.AppendLine($"# Audit trail of this API call:");
			var auditTrail = (r.AuditTrail ?? Enumerable.Empty<Audit>()).ToList();
			DebugAuditTrail(auditTrail, sb);
			if (r.OriginalException != null) sb.AppendLine($"# OriginalException: {r.OriginalException}");
			DebugAuditTrailExceptions(auditTrail, sb);

			var response = r.ResponseBodyInBytes?.Utf8String() ?? ResponseAlreadyCaptured;
			var request = r.RequestBodyInBytes?.Utf8String() ?? RequestAlreadyCaptured;
			sb.AppendLine($"# Request:{Environment.NewLine}{request}");
			sb.AppendLine($"# Response:{Environment.NewLine}{response}");

			if (r.TcpStats != null)
			{
				sb.AppendLine("# TCP states:");
				foreach (var stat in r.TcpStats)
				{
					sb.Append("  ");
					sb.Append(stat.Key);
					sb.Append(": ");
					sb.AppendLine($"{stat.Value}");
				}
				sb.AppendLine();
			}

			if (r.ThreadPoolStats != null)
			{
				sb.AppendLine("# ThreadPool statistics:");
				foreach (var stat in r.ThreadPoolStats)
				{
					sb.Append("  ");
					sb.Append(stat.Key);
					sb.AppendLine(": ");
					sb.Append("    Busy: ");
					sb.AppendLine($"{stat.Value.Busy}");
					sb.Append("    Free: ");
					sb.AppendLine($"{stat.Value.Free}");
					sb.Append("    Min: ");
					sb.AppendLine($"{stat.Value.Min}");
					sb.Append("    Max: ");
					sb.AppendLine($"{stat.Value.Max}");
				}
				sb.AppendLine();
			}

			return sb.ToString();
		}

		/// <summary>
		/// Write the exceptions recorded in <paramref name="auditTrail"/> to <paramref name="sb"/> in
		/// a debuggable and human readable string
		/// </summary>
		public static void DebugAuditTrailExceptions(IReadOnlyCollection<Audit> auditTrail, StringBuilder sb)
		{
			if (auditTrail == null) return;

			var auditExceptions = auditTrail.Select((audit, i) => new { audit, i }).Where(a => a.audit.Exception != null);
			foreach (var a in auditExceptions)
				sb.AppendLine($"# Audit exception in step {a.i + 1} {a.audit.Event.GetStringValue()}:{Environment.NewLine}{a.audit.Exception}");
		}

		/// <summary>
		/// Write the events recorded in <paramref name="auditTrail"/> to <paramref name="sb"/> in
		/// a debuggable and human readable string
		/// </summary>
		public static void DebugAuditTrail(IReadOnlyCollection<Audit> auditTrail, StringBuilder sb)
		{
			if (auditTrail == null) return;

			foreach (var a in auditTrail.Select((a, i) => new { a, i }))
			{
				var audit = a.a;
				sb.Append($" - [{a.i + 1}] {audit.Event.GetStringValue()}:");

				AuditNodeUrl(sb, audit);

				if (audit.Exception != null) sb.Append($" Exception: {audit.Exception.GetType().Name}");
				if (audit.Ended == default)
					sb.AppendLine();
				else sb.AppendLine($" Took: {(audit.Ended - audit.Started).ToString()}");
			}
		}

		private static void AuditNodeUrl(StringBuilder sb, Audit audit)
		{
			var uri = audit.Node?.Uri;
			if (uri == null) return;

			if (!string.IsNullOrEmpty(uri.UserInfo))
			{
				var builder = new UriBuilder(uri)
				{
					Password = "redacted"
				};
				uri = builder.Uri;
			}
			sb.Append($" Node: {uri}");
		}
	}
}
