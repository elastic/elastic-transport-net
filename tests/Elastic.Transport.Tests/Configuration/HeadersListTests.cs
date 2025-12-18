// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Linq;
using FluentAssertions;
using Xunit;

namespace Elastic.Transport.Tests.Configuration
{
	public class HeadersListTests
	{
		[Fact]
		public void SupportsEnumerationWhenEmpty()
		{
			var sut = new HeadersList();

			foreach (var header in sut)
			{
			}
		}

		[Fact]
		public void CtorSkipsDuplicatesFromSingleEnumerable()
		{
			var sut = new HeadersList(["header-one", "header-two", "header-TWO"]);

			sut.Count.Should().Be(2);
			sut.First().Should().Be("header-one");
			sut.Last().Should().Be("header-two");
		}

		[Fact]
		public void CtorSkipsDuplicatesFromSingleEnumerableAndSingleHeader()
		{
			var sut = new HeadersList(["header-one", "header-two"], "header-TWO");

			sut.Count.Should().Be(2);
			sut.First().Should().Be("header-one");
			sut.Last().Should().Be("header-two");
		}

		[Fact]
		public void CtorSkipsDuplicatesFromTwoEnumerables()
		{
			var sut = new HeadersList(["header-ONE", "header-two"], ["header-one", "header-THREE", "HEADER-TWO"]);

			sut.Count.Should().Be(3);

			var count = 0;
			foreach (var header in sut)
			{
				count++;

				switch (count)
				{
					case 1:
						header.Should().Be("header-ONE");
						break;
					case 2:
						header.Should().Be("header-two");
						break;
					case 3:
						header.Should().Be("header-THREE");
						break;
				}
			}
		}
	}
}
