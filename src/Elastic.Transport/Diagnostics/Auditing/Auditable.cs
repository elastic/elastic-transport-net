// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;

namespace Elastic.Transport.Diagnostics.Auditing;

internal class Auditable : IDisposable
{
	private readonly Audit _audit;

	private readonly DateTimeProvider _dateTimeProvider;

	public Auditable(AuditEvent type, ref List<Audit> auditTrail, DateTimeProvider dateTimeProvider, Node node)
	{
		auditTrail ??= new List<Audit>();

		_dateTimeProvider = dateTimeProvider;

		var started = _dateTimeProvider.Now();

		_audit = new Audit(type, started)
		{
			Node = node
		};

		auditTrail.Add(_audit);
	}

	public AuditEvent Event
	{
		set => _audit.Event = value;
	}

	public Exception Exception
	{
		set => _audit.Exception = value;
	}

	public string PathAndQuery
	{
		set => _audit.PathAndQuery = value;
	}

	public void Dispose() => _audit.Ended = _dateTimeProvider.Now();
}
