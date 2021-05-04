// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Diagnostics;
using Elastic.Transport.Extensions;

namespace Elastic.Transport.Diagnostics.Auditing
{
	internal static class AuditEventExtensions
	{
		/// <summary>
		/// Returns the name of the event to be used for use in <see cref="DiagnosticSource"/>.
		/// <para>If this return null the event should not be reported on</para>
		/// <para>This indicates this event is monitored by a different component already</para>
		/// </summary>
		/// <returns>The diagnostic event name representation or null if it should go unreported</returns>
		public static string GetAuditDiagnosticEventName(this AuditEvent @event)
		{
			switch(@event)
			{
				case AuditEvent.SniffFailure:
				case AuditEvent.SniffSuccess:
				case AuditEvent.PingFailure:
				case AuditEvent.PingSuccess:
				case AuditEvent.BadResponse:
				case AuditEvent.HealthyResponse:
					return null;
				case AuditEvent.SniffOnStartup: return nameof(AuditEvent.SniffOnStartup);
				case AuditEvent.SniffOnFail: return nameof(AuditEvent.SniffOnFail);
				case AuditEvent.SniffOnStaleCluster: return nameof(AuditEvent.SniffOnStaleCluster);
				case AuditEvent.Resurrection: return nameof(AuditEvent.Resurrection);
				case AuditEvent.AllNodesDead: return nameof(AuditEvent.AllNodesDead);
				case AuditEvent.MaxTimeoutReached: return nameof(AuditEvent.MaxTimeoutReached);
				case AuditEvent.MaxRetriesReached: return nameof(AuditEvent.MaxRetriesReached);
				case AuditEvent.BadRequest: return nameof(AuditEvent.BadRequest);
				case AuditEvent.NoNodesAttempted: return nameof(AuditEvent.NoNodesAttempted);
				case AuditEvent.CancellationRequested: return nameof(AuditEvent.CancellationRequested);
				case AuditEvent.FailedOverAllNodes: return nameof(AuditEvent.FailedOverAllNodes);
				default: return @event.GetStringValue(); //still cached but uses reflection
			}
		}


	}
}
