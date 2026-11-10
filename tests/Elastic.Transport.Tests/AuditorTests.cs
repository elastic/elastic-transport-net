// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Linq;
using Elastic.Transport.Diagnostics.Auditing;
using FluentAssertions;
using Xunit;
using static Elastic.Transport.Diagnostics.Auditing.AuditEvent;

namespace Elastic.Transport.Tests;

public class AuditorTests
{
	private static Auditor Auditor() => new(DefaultDateTimeProvider.Default);

	[Fact]
	public void AnEmptyTrailIsEmpty()
	{
		var auditor = Auditor();

		_ = auditor.Count.Should().Be(0);
		_ = auditor.ToList().Should().BeEmpty();
	}

	[Fact]
	public void OneEntryIsCountedAndEnumerated()
	{
		var auditor = Auditor();
		auditor.Emit(HealthyResponse);

		_ = auditor.Count.Should().Be(1);
		_ = auditor.Select(a => a.Event).Should().Equal(HealthyResponse);
	}

	[Fact]
	public void SeveralEntriesKeepTheirOrderAndCanBeEnumeratedRepeatedly()
	{
		var auditor = Auditor();
		auditor.Emit(SniffOnStartup);
		auditor.Emit(PingSuccess);
		auditor.Emit(BadResponse);
		auditor.Emit(HealthyResponse);

		_ = auditor.Count.Should().Be(4);
		_ = auditor.Select(a => a.Event).Should().Equal(SniffOnStartup, PingSuccess, BadResponse, HealthyResponse);
		_ = auditor.Select(a => a.Event).Should().Equal(SniffOnStartup, PingSuccess, BadResponse, HealthyResponse);
	}
}
