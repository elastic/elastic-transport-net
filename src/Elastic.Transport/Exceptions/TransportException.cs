// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Elastic.Transport.Diagnostics.Auditing;
using Elastic.Transport.Extensions;
using static Elastic.Transport.Diagnostics.ResponseStatics;

namespace Elastic.Transport;

/// <summary>
/// Exceptions that occur are wrapped inside this exception. This is done to not lose valuable diagnostic information.
/// <para>
/// When <see cref="IRequestConfiguration.ThrowExceptions"/> is set these exceptions are rethrown and need
/// to be caught
/// </para>
/// </summary>
public class TransportException : Exception
{
	/// <inheritdoc cref="TransportException"/>
	public TransportException(string message) : base(message) => FailureReason = PipelineFailure.Unexpected;

	/// <inheritdoc cref="TransportException"/>
	public TransportException(PipelineFailure failure, string message, Exception? innerException = null)
		: base(message, innerException) => FailureReason = failure;

	/// <inheritdoc cref="TransportException"/>
	public TransportException(PipelineFailure failure, string message, TransportResponse response)
		: this(message)
	{
		ApiCallDetails = response.ApiCallDetails;
		FailureReason = failure;
		AuditTrail = response.ApiCallDetails?.AuditTrail;
	}

	/// <summary>
	/// The audit trail keeping track of what happened during the invocation of
	/// a request, up until the moment of this exception.
	/// </summary>
	public IReadOnlyCollection<Audit>? AuditTrail { get; internal init; }

	/// <summary>
	/// The reason this exception occurred was one of the well defined exit points as modelled by
	/// <see cref="PipelineFailure"/>
	/// </summary>
	// ReSharper disable once MemberCanBePrivate.Global
	public PipelineFailure? FailureReason { get; }

	/// <summary> Information about the request that triggered this exception </summary>
	public Endpoint? Endpoint { get; internal set; }

	/// <summary> The response if available that triggered the exception </summary>
	public ApiCallDetails? ApiCallDetails { get; internal set; }

	/// <summary>
	/// A self describing human readable string explaining why this exception was thrown.
	/// <para>Useful in logging and diagnosing + reporting issues!</para>
	/// </summary>
	// ReSharper disable once UnusedMember.Global
	public string DebugInformation
	{
		get
		{
			var sb = new StringBuilder();
			var failureReason = FailureReason is { } r ? r.ToStringFast() : string.Empty;
			if (FailureReason == PipelineFailure.Unexpected && AuditTrail.HasAny(out var auditTrail) && auditTrail!.Length > 0)
				failureReason = "Unrecoverable/Unexpected " + auditTrail[^1].Event.ToStringFast();

			_ = sb.Append("# FailureReason: ")
				.Append(failureReason)
				.Append(" while attempting ");

			if (Endpoint is not null)
			{
				_ = sb.Append(Endpoint.Method.GetStringValue()).Append(" on ");
				_ = sb.AppendLine(Endpoint.Uri.ToString());
			}
			else
			{
				_ = ApiCallDetails is not null
					? sb.Append(ApiCallDetails.HttpMethod.GetStringValue())
					.Append(" on ")
					.AppendLine(ApiCallDetails.Uri?.ToString())
					: sb.AppendLine("a request");
			}

			if (ApiCallDetails is not null)
				_ = DebugInformationBuilder(ApiCallDetails, sb);
			else if (AuditTrail is not null)
			{
				DebugAuditTrail(AuditTrail, sb);
				DebugAuditTrailExceptions(AuditTrail, sb);
			}

			if (InnerException != null)
			{
				_ = sb.Append("# Inner Exception: ")
					.AppendLine(InnerException.Message)
					.AppendLine(InnerException.ToString());
			}

			_ = sb.AppendLine("# Exception:")
				.AppendLine(ToString());
			return sb.ToString();
		}
	}
}
