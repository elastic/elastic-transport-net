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
		public void TryAdd_SkipsDuplicates()
		{
			var sut = new HeadersList(new[] { "header-one", "header-two" });

			sut.TryAdd("header-one");
			sut.TryAdd("header-TWO");

			sut.Count.Should().Be(2);
			sut.First().Should().Be("header-one");
			sut.Last().Should().Be("header-two");
		}

		[Fact]
		public void TryAdd_RemovesExpectedHeader()
		{
			var sut = new HeadersList(new[] { "header-one", "header-two" });

			sut.Remove("header-one");

			sut.Count.Should().Be(1);
			sut.Single().Should().Be("header-two");
		}

		[Fact]
		public void TryAdd_RemovesAreCaseInsensitive()
		{
			var sut = new HeadersList(new[] { "header-one", "header-two" });

			sut.Remove("header-ONE");

			sut.Count.Should().Be(1);
			sut.Single().Should().Be("header-two");
		}

		[Fact]
		public void TryAdd_UnionWithSkipsDuplicates()
		{
			var headers = new HeadersList(new[] { "header-ONE", "header-THREE" });

			var sut = new HeadersList(new[] { "header-one", "header-two" });
			sut.UnionWith(headers);

			sut.Count.Should().Be(3);

			var count = 0;
			foreach (var header in sut)
			{
				count++;

				switch (count)
				{
					case 1:
						header.Should().Be("header-one");
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
