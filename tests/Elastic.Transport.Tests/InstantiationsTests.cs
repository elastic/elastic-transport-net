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
			var p = PostData.MultiJson(new [] {new A()});
			p.Type.Should().Be(PostType.EnumerableOfObject);
		}

		[Fact]
		public void StringMultiJson()
		{
			var p = PostData.MultiJson(new [] {""});
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
