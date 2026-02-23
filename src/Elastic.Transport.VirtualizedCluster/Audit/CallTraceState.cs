// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using Elastic.Transport.Diagnostics.Auditing;

namespace Elastic.Transport.VirtualizedCluster.Audit;

public sealed class CallTraceState(AuditEvent e)
{
	public Action<string, Transport.Diagnostics.Auditing.Audit> AssertWithBecause { get; set; }

	public AuditEvent Event { get; private set; } = e;

	public int? Port { get; set; }

	public Action<Transport.Diagnostics.Auditing.Audit> SimpleAssert { get; set; }
}
