// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;

namespace Elastic.Transport.Diagnostics.Auditing;

internal class Auditable : IDisposable
{
	private readonly Audit _audit;

	private readonly DateTimeProvider _dateTimeProvider;

	public Auditable(AuditEvent type, DateTimeProvider dateTimeProvider, Node? node)
	{
		_dateTimeProvider = dateTimeProvider;

		var started = _dateTimeProvider.Now();
		_audit = new Audit(type, started)
		{
			Node = node
		};
	}

	public AuditEvent Event
	{
		set => Audit.Event = value;
	}

	public Exception Exception
	{
		set => Audit.Exception = value;
	}

	public Audit Audit => _audit;

	public void Dispose() => Audit.Ended = _dateTimeProvider.Now();
}
