// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Collections;
using System.Collections.Generic;
using Elastic.Transport.Extensions;

namespace Elastic.Transport.Diagnostics.Auditing;

/// Collects <see cref="Audit"/> events
#pragma warning disable CA1710 // Identifiers should have correct suffix
public class Auditor : IReadOnlyCollection<Audit>
#pragma warning restore CA1710 // Identifiers should have correct suffix
{
	private readonly DateTimeProvider _dateTimeProvider;
	// Most requests record a single audit (the healthy response), so the first one lives in a field and the list, with
	// its backing array, is only created for the second: about 90 B saved on the common path.
	private Audit? _first;
	private List<Audit>? _more;

	internal Auditor(DateTimeProvider dateTimeProvider) => _dateTimeProvider = dateTimeProvider;

	/// <inheritdoc cref="IEnumerable{T}.GetEnumerator"/>
	public IEnumerator<Audit> GetEnumerator() =>
		_first is null ? new EmptyEnumerator<Audit>() : Enumerate();

	private IEnumerator<Audit> Enumerate()
	{
		yield return _first!;

		if (_more is null)
			yield break;

		foreach (var audit in _more)
			yield return audit;
	}

	private void Append(Audit audit)
	{
		if (_first is null)
			_first = audit;
		else
			(_more ??= []).Add(audit);
	}

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

	internal Auditable Add(Auditable auditable)
	{
		Append(auditable.Audit);
		return auditable;
	}

	internal Auditable Add(AuditEvent type, DateTimeProvider dateTimeProvider, Node? node = null)
	{
		var auditable = new Auditable(type, dateTimeProvider, node);
		Append(auditable.Audit);
		return auditable;
	}

	/// Emits an event that does not need to track a duration
	public void Emit(AuditEvent type) => Add(type, _dateTimeProvider).Dispose();

	/// Emits an event that does not need to track a duration
	public void Emit(AuditEvent type, Node node) => Add(type, _dateTimeProvider, node).Dispose();

	/// <inheritdoc cref="IReadOnlyCollection{T}.Count"/>
	public int Count => (_first is null ? 0 : 1) + (_more?.Count ?? 0);
}
