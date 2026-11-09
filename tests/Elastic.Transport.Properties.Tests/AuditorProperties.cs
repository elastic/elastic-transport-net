// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Linq;
using CsCheck;
using Elastic.Transport.Diagnostics.Auditing;
using FluentAssertions;
using Xunit;

namespace Elastic.Transport.Properties.Tests;

public class AuditorProperties
{
	[Fact]
	public void ATrailKeepsEveryEventInOrderWhateverItsLength() =>
		Gen.Enum<AuditEvent>().Array[0, 30].Sample(events =>
		{
			var auditor = new Auditor(DefaultDateTimeProvider.Default);
			foreach (var e in events)
				auditor.Emit(e);

			_ = auditor.Count.Should().Be(events.Length);
			_ = auditor.Select(a => a.Event).Should().Equal(events);
			_ = auditor.Select(a => a.Event).Should().Equal(events, "enumerating twice gives the same trail");
		}, iter: 500);
}
