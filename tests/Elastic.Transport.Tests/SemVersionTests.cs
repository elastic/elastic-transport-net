// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.Transport.Extensions;
using FluentAssertions;
using Xunit;

namespace Elastic.Transport.Tests;

public class SemVersionTests
{
	[Fact]
	public void AVersionWithAPreReleaseComparesEqualToAnIdenticalCopy()
	{
		// CompareByPrecedence used to end by comparing this.Prerelease with other.Metadata.
		var a = new SemVersion(1, 0, 0, "alpha.1");
		var b = new SemVersion(1, 0, 0, "alpha.1");

		_ = a.CompareByPrecedence(b).Should().Be(0);
		_ = a.CompareTo(b).Should().Be(0);
		_ = a.Equals(b).Should().BeTrue();
	}

	[Fact]
	public void BuildMetadataDoesNotChangePrecedenceButDoesDistinguishVersions()
	{
		var plain = new SemVersion(1, 2, 3, "rc.1");
		var built = new SemVersion(1, 2, 3, "rc.1", "build.5");

		_ = plain.CompareByPrecedence(built).Should().Be(0);
		_ = plain.CompareTo(built).Should().BeLessThan(0, "metadata only breaks the tie, with none first");
		_ = plain.Equals(built).Should().BeFalse();
	}

	[Fact]
	public void APreReleaseComesBeforeItsRelease() =>
		_ = new SemVersion(1, 0, 0, "rc.1").CompareByPrecedence(new SemVersion(1, 0, 0)).Should().BeLessThan(0);
}
