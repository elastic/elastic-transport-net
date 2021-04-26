/*
 * Licensed to Elasticsearch B.V. under one or more contributor
 * license agreements. See the NOTICE file distributed with
 * this work for additional information regarding copyright
 * ownership. Elasticsearch B.V. licenses this file to you under
 * the Apache License, Version 2.0 (the "License"); you may
 * not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *    http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing,
 * software distributed under the License is distributed on an
 * "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
 * KIND, either express or implied.  See the License for the
 * specific language governing permissions and limitations
 * under the License.
 */

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
