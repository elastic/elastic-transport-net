// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Elastic.Transport.Diagnostics.Auditing;
using Elastic.Transport.Extensions;
using static Elastic.Transport.ResponseStatics;

namespace Elastic.Transport
{

	/// <summary>
	/// Exceptions that occur <see cref="ITransport.Request{TResponse}"/> are wrapped inside
	/// this exception. This is done to not lose valuable diagnostic information.
	///
	/// <para>
	/// When <see cref="ITransportConfiguration.ThrowExceptions"/> is set these exceptions are rethrown and need
	/// to be caught
	/// </para>
	/// </summary>
	public class TransportException : Exception
	{
		/// <inheritdoc cref="TransportException"/>
		public TransportException(string message) : base(message) => FailureReason = PipelineFailure.Unexpected;

		/// <inheritdoc cref="TransportException"/>
		public TransportException(PipelineFailure failure, string message, Exception innerException)
			: base(message, innerException) => FailureReason = failure;

		/// <inheritdoc cref="TransportException"/>
		public TransportException(PipelineFailure failure, string message, IApiCallDetails apiCall)
			: this(message)
		{
			Response = apiCall;
			FailureReason = failure;
			AuditTrail = apiCall?.AuditTrail;
		}

		/// <summary>
		/// The audit trail keeping track of what happened during the invocation of
		/// <see cref="ITransport.Request{TResponse}"/> up until the moment of this exception
		/// </summary>
		public IEnumerable<Audit> AuditTrail { get; internal set; }

		/// <summary>
		/// The reason this exception occurred was one of the well defined exit points as modelled by
		/// <see cref="PipelineFailure"/>
		/// </summary>
		// ReSharper disable once MemberCanBePrivate.Global
		public PipelineFailure? FailureReason { get; }

		/// <summary> Information about the request that triggered this exception </summary>
		public RequestData Request { get; internal set; }

		/// <summary> The response if available that triggered the exception </summary>
		public IApiCallDetails Response { get; internal set; }

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
				var failureReason = FailureReason.GetStringValue();
				if (FailureReason == PipelineFailure.Unexpected && AuditTrail.HasAny(out var auditTrail))
					failureReason = "Unrecoverable/Unexpected " + auditTrail.Last().Event.GetStringValue();

				sb.Append("# FailureReason: ")
					.Append(failureReason)
					.Append(" while attempting ");

				if (Request != null)
				{
					sb.Append(Request.Method.GetStringValue()).Append(" on ");
					if (Request.Uri != null)
						sb.AppendLine(Request.Uri.ToString());
					else
					{
						sb.Append(Request.PathAndQuery)
							.AppendLine(" on an empty node, likely a node predicate on ConnectionSettings not matching ANY nodes");
					}
				}
				else if (Response != null)
				{
					sb.Append(Response.HttpMethod.GetStringValue())
						.Append(" on ")
						.AppendLine(Response.Uri.ToString());
				}
				else
					sb.AppendLine("a request");

				if (Response != null)
					DebugInformationBuilder(Response, sb);
				else
				{
					DebugAuditTrail(AuditTrail, sb);
					DebugAuditTrailExceptions(AuditTrail, sb);
				}

				if (InnerException != null)
				{
					sb.Append("# Inner Exception: ")
						.AppendLine(InnerException.Message)
						.AppendLine(InnerException.ToString());
				}

				sb.AppendLine("# Exception:")
					.AppendLine(ToString());
				return sb.ToString();
			}
		}
	}
}
