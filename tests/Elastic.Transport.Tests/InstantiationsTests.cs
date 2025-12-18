// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using FluentAssertions;
using Xunit;

namespace Elastic.Transport.Tests
{
	public class InstantiationsTests
	{
		public class A {}

		[Fact]
		public void SerializableMultiJson()
		{
			var p = PostData.MultiJson([new A()]);
			p.Type.Should().Be(PostType.EnumerableOfObject);
		}

		[Fact]
		public void StringMultiJson()
		{
			var p = PostData.MultiJson([""]);
			p.Type.Should().Be(PostType.EnumerableOfString);
		}

		[Fact]
		public void ObjectMultiJson()
		{
			var p = PostData.MultiJson(new object[] {new A()});
			p.Type.Should().Be(PostType.EnumerableOfObject);
		}
	}
}
