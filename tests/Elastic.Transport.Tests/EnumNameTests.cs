// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Runtime.Serialization;
using Elastic.Transport.Extensions;
using FluentAssertions;
using Xunit;

namespace Elastic.Transport.Tests;

public class EnumNameTests
{
	private enum Refresh
	{
		[EnumMember(Value = "true")] True,
		[EnumMember(Value = "wait_for")] WaitFor,
		NoAttribute,
	}

	// Same underlying values as Refresh, different names: a cache keyed on the number alone would mix them up.
	private enum Conflicts
	{
		[EnumMember(Value = "other")] Other,
		Plain,
		[EnumMember(Value = "third")] Third,
	}

	[Fact]
	public void UsesTheEnumMemberValueWhenThereIsOne() =>
		((Enum)Refresh.WaitFor).GetEnumName().Should().Be("wait_for");

	[Fact]
	public void FallsBackToTheMemberName() =>
		((Enum)Refresh.NoAttribute).GetEnumName().Should().Be("NoAttribute");

	[Fact]
	public void DifferentEnumTypesWithTheSameNumberDoNotShareAnAnswer()
	{
		// Ask in both orders, twice, so a collision would show whichever type populated the cache first.
		for (var i = 0; i < 2; i++)
		{
			((Enum)Refresh.True).GetEnumName().Should().Be("true");
			((Enum)Conflicts.Other).GetEnumName().Should().Be("other");
			((Enum)Refresh.WaitFor).GetEnumName().Should().Be("wait_for");
			((Enum)Conflicts.Plain).GetEnumName().Should().Be("Plain");
		}
	}
}
