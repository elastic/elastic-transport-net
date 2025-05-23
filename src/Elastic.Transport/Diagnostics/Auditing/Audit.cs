// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using Elastic.Transport.Extensions;

namespace Elastic.Transport.Diagnostics.Auditing;

/// <summary> An audit of the request made </summary>
public sealed class Audit
{
	internal Audit(AuditEvent type, DateTimeOffset started)
	{
		Event = type;
		Started = started;
	}

	/// <summary>
	/// The type of audit event.
	/// </summary>
	public AuditEvent Event { get; internal set; }

	/// <summary>
	/// The node on which the request was made.
	/// </summary>
	public Node? Node { get; internal init; }

	/// <summary>
	/// The end date and time of the audit.
	/// </summary>
	public DateTimeOffset Ended { get; internal set; }

	/// <summary>
	/// The start date and time of the audit.
	/// </summary>
	public DateTimeOffset Started { get; }

	/// <summary>
	/// The exception for the audit, if there was one.
	/// </summary>
	public Exception Exception { get; internal set; }

	/// <summary>
	/// Returns a string representation of the this audit.
	/// </summary>
	public override string ToString()
	{
		var took = Ended - Started;
		var tookString = string.Empty;
		if (took >= TimeSpan.Zero) tookString = $" Took: {took}";

		return Node == null
			? $"Event: {Event.ToStringFast()}{tookString}"
			: $"Event: {Event.ToStringFast()} Node: {Node?.Uri} NodeAlive: {Node?.IsAlive}Took: {tookString}";
	}
}
