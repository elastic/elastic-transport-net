// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections;
using System.Collections.Generic;
using Elastic.Transport.Extensions;

namespace Elastic.Transport.Diagnostics.Auditing;

/// Collects <see cref="Audit"/> events
public class Auditor : IReadOnlyCollection<Audit>
{
	private readonly DateTimeProvider _dateTimeProvider;
	private List<Audit>? _audits;

	internal Auditor(DateTimeProvider dateTimeProvider) => _dateTimeProvider = dateTimeProvider;

	/// <inheritdoc cref="IEnumerable{T}.GetEnumerator"/>
	public IEnumerator<Audit> GetEnumerator() =>
		_audits?.GetEnumerator() ?? (IEnumerator<Audit>)new EmptyEnumerator<Audit>();

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

	internal Auditable Add(Auditable auditable)
	{
		_audits ??= new List<Audit>();
		_audits.Add(auditable.Audit);
		return auditable;
	}
	internal Auditable Add(AuditEvent type, DateTimeProvider dateTimeProvider, Node? node = null)
	{
		_audits ??= new List<Audit>();
		var auditable = new Auditable(type, dateTimeProvider, node);
		_audits.Add(auditable.Audit);
		return auditable;
	}

	/// Emits an event that does not need to track a duration
	public void Emit(AuditEvent type) => Add(type, _dateTimeProvider).Dispose();
	/// Emits an event that does not need to track a duration
	public void Emit(AuditEvent type, Node node) => Add(type, _dateTimeProvider, node).Dispose();

	/// <inheritdoc cref="IReadOnlyCollection{T}.Count"/>
	public int Count => _audits?.Count ?? 0;
}

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

	public string PathAndQuery
	{
		set => Audit.PathAndQuery = value;
	}

	public Audit Audit => _audit;

	public void Dispose() => Audit.Ended = _dateTimeProvider.Now();
}
