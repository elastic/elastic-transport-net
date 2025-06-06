// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;

namespace Elastic.Transport.Diagnostics.Auditing;

/// <summary>
/// Enumeration of different auditable events that can occur in the execution of
/// requests as modelled by <see cref="RequestPipeline"/>.
/// </summary>
public enum AuditEvent
{
	/// <summary> The request performed the first sniff on startup of the client </summary>
	SniffOnStartup,

	/// <summary> The request saw a failure on a node and a sniff occurred as a result of it</summary>
	SniffOnFail,

	/// <summary> The cluster state expired and a sniff occurred as a result of it</summary>
	SniffOnStaleCluster,

	/// <summary>A sniff that was initiated was successful</summary>
	SniffSuccess,

	/// <summary>A sniff that was initiated resulted in a failure</summary>
	SniffFailure,

	/// <summary>A ping that was initiated was successful</summary>
	PingSuccess,

	/// <summary>A ping that was initiated resulted in a failure</summary>
	PingFailure,

	/// <summary>A node that was previously marked dead was put back in the regular rotation</summary>
	Resurrection,

	/// <summary>
	/// All nodes returned by <see cref="NodePool.CreateView"/> are marked dead (see <see cref="Node.IsAlive"/>)
	/// After this event a random node is resurrected and tried by force
	/// </summary>
	AllNodesDead,

	/// <summary>
	/// A call into <see cref="RequestPipeline.CallProductEndpointAsync{TResponse}"/> resulted in a failure
	/// </summary>
	BadResponse,

	/// <summary>
	/// A call into <see cref="RequestPipeline.CallProductEndpointAsync{TResponse}"/> resulted in a success
	/// </summary>
	HealthyResponse,

	/// <summary>
	/// The request took too long.
	/// This could mean the call was retried but retrying was to slow and cumulatively this exceeded
	/// <see cref="IRequestConfiguration.MaxRetryTimeout"/>
	/// </summary>
	MaxTimeoutReached,

	/// <summary>
	/// The request was not able to complete
	/// successfully and exceeded the available retries as configured on
	/// <see cref="IRequestConfiguration.MaxRetries"/>.
	/// </summary>
	MaxRetriesReached,

	/// <summary>
	/// A call into <see cref="RequestPipeline.CallProductEndpointAsync{TResponse}"/> failed before a response was
	/// received.
	/// </summary>
	BadRequest,

	/// <summary>
	/// Rare but if <see cref="ITransportConfiguration.NodePredicate"/> is too stringent and node nodes in
	/// the <see cref="NodePool.Nodes"/> satisfies this predicate this will result in this failure.
	/// </summary>
	NoNodesAttempted,

	/// <summary>
	/// Signals the audit may be incomplete because cancellation was requested on the async paths
	/// </summary>
	CancellationRequested,

	/// <summary>
	/// The request failed within the allotted <see cref="IRequestConfiguration.MaxRetryTimeout"/> but failed
	/// on all the available <see cref="NodePool.Nodes"/>
	/// </summary>
	FailedOverAllNodes,
}

internal static class AuditEventExtensions
{
	public static string ToStringFast(this AuditEvent auditEvent) => auditEvent switch
	{
		AuditEvent.SniffOnStartup => nameof(AuditEvent.SniffOnStartup),
		AuditEvent.SniffOnFail => nameof(AuditEvent.SniffOnFail),
		AuditEvent.SniffOnStaleCluster => nameof(AuditEvent.SniffOnStaleCluster),
		AuditEvent.SniffSuccess => nameof(AuditEvent.SniffSuccess),
		AuditEvent.SniffFailure => nameof(AuditEvent.SniffFailure),
		AuditEvent.PingSuccess => nameof(AuditEvent.PingSuccess),
		AuditEvent.PingFailure => nameof(AuditEvent.PingFailure),
		AuditEvent.Resurrection => nameof(AuditEvent.Resurrection),
		AuditEvent.AllNodesDead => nameof(AuditEvent.AllNodesDead),
		AuditEvent.BadResponse => nameof(AuditEvent.BadResponse),
		AuditEvent.HealthyResponse => nameof(AuditEvent.HealthyResponse),
		AuditEvent.MaxTimeoutReached => nameof(AuditEvent.MaxTimeoutReached),
		AuditEvent.MaxRetriesReached => nameof(AuditEvent.MaxRetriesReached),
		AuditEvent.BadRequest => nameof(AuditEvent.BadRequest),
		AuditEvent.NoNodesAttempted => nameof(AuditEvent.NoNodesAttempted),
		AuditEvent.CancellationRequested => nameof(AuditEvent.CancellationRequested),
		AuditEvent.FailedOverAllNodes => nameof(AuditEvent.FailedOverAllNodes),
		_ => throw new ArgumentOutOfRangeException(nameof(auditEvent), auditEvent, null)
	};

}

