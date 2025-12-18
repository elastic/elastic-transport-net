// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Elastic.Transport.Diagnostics.Auditing;
using Elastic.Transport.Extensions;

namespace Elastic.Transport.Diagnostics;

/// <summary>
/// Creates human readable debug strings based on <see cref="ApiCallDetails"/> so that
/// its clear what exactly transpired during a request.
/// </summary>
internal static class ResponseStatics
{
	private static readonly string RequestAlreadyCaptured =
		"<Request stream not captured or already read to completion by serializer. Set DisableDirectStreaming() on TransportConfiguration to force it to be set on the response.>";

	private static readonly string ResponseAlreadyCaptured =
		"<Response stream not captured or already read to completion by serializer. Set DisableDirectStreaming() on TransportConfiguration to force it to be set on the response.>";

	/// <inheritdoc cref="ResponseStatics"/>
	public static string DebugInformationBuilder(ApiCallDetails r, StringBuilder sb)
	{

		_ = sb.AppendLine($"# Audit trail of this API call:");

		var auditTrail = (r.AuditTrail ?? Enumerable.Empty<Audit>()).ToList();

		if (!r.TransportConfiguration.DisableAuditTrail ?? true)
		{
			DebugAuditTrail(auditTrail, sb);
		}
		else
		{
			_ = sb.AppendLine("<Audit trail not captured. Set DisableAuditTrail(false) on TransportConfiguration to capture it.>");
		}

		if (r.OriginalException != null)
			_ = sb.AppendLine($"# OriginalException: {r.OriginalException}");

		if (!r.TransportConfiguration.DisableAuditTrail ?? true)
			DebugAuditTrailExceptions(auditTrail, sb);

		var response = r.ResponseBodyInBytes?.Utf8String() ?? ResponseAlreadyCaptured;
		var request = r.RequestBodyInBytes?.Utf8String() ?? RequestAlreadyCaptured;
		_ = sb.AppendLine($"# Request:{Environment.NewLine}{request}");
		_ = sb.AppendLine($"# Response:{Environment.NewLine}{response}");

		if (r.TcpStats != null)
		{
			_ = sb.AppendLine("# TCP states:");
			foreach (var stat in r.TcpStats)
			{
				_ = sb.Append("  ");
				_ = sb.Append(stat.Key);
				_ = sb.Append(": ");
				_ = sb.AppendLine($"{stat.Value}");
			}
			_ = sb.AppendLine();
		}

		if (r.ThreadPoolStats != null)
		{
			_ = sb.AppendLine("# ThreadPool statistics:");
			foreach (var stat in r.ThreadPoolStats)
			{
				_ = sb.Append("  ");
				_ = sb.Append(stat.Key);
				_ = sb.AppendLine(": ");
				_ = sb.Append("    Busy: ");
				_ = sb.AppendLine($"{stat.Value.Busy}");
				_ = sb.Append("    Free: ");
				_ = sb.AppendLine($"{stat.Value.Free}");
				_ = sb.Append("    Min: ");
				_ = sb.AppendLine($"{stat.Value.Min}");
				_ = sb.Append("    Max: ");
				_ = sb.AppendLine($"{stat.Value.Max}");
			}
			_ = sb.AppendLine();
		}

		return sb.ToString();
	}

	/// <summary>
	/// Write the exceptions recorded in <paramref name="auditTrail"/> to <paramref name="sb"/> in
	/// a debuggable and human readable string
	/// </summary>
	public static void DebugAuditTrailExceptions(IEnumerable<Audit> auditTrail, StringBuilder sb)
	{
		if (auditTrail == null)
			return;

		var auditExceptions = auditTrail.Select((audit, i) => new { audit, i }).Where(a => a.audit.Exception != null);
		foreach (var a in auditExceptions)
			_ = sb.AppendLine($"# Audit exception in step {a.i + 1} {a.audit.Event.ToStringFast()}:{Environment.NewLine}{a.audit.Exception}");
	}

	/// <summary>
	/// Write the events recorded in <paramref name="auditTrail"/> to <paramref name="sb"/> in
	/// a debuggable and human readable string
	/// </summary>
	public static void DebugAuditTrail(IEnumerable<Audit> auditTrail, StringBuilder sb)
	{
		if (auditTrail == null)
			return;

		foreach (var a in auditTrail.Select((a, i) => new { a, i }))
		{
			var audit = a.a;
			_ = sb.Append($" - [{a.i + 1}] {audit.Event.ToStringFast()}:");

			AuditNodeUrl(sb, audit);

			if (audit.Exception != null)
				_ = sb.Append($" Exception: {audit.Exception.GetType().Name}");
			if (audit.Ended == default)
				_ = sb.AppendLine();
			else
				_ = sb.AppendLine($" Took: {audit.Ended - audit.Started}");
		}
	}

	private static void AuditNodeUrl(StringBuilder sb, Audit audit)
	{
		var uri = audit.Node?.Uri;
		if (uri == null)
			return;

		if (!string.IsNullOrEmpty(uri.UserInfo))
		{
			var builder = new UriBuilder(uri)
			{
				Password = "redacted"
			};
			uri = builder.Uri;
		}
		_ = sb.Append($" Node: {uri}");
	}
}
