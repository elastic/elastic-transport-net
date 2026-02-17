// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.IO;
using System.Text;
using FluentAssertions;
using Xunit;

namespace Elastic.Transport.Tests.Responses.Dynamic;

public class DynamicDictionaryGetTests
{
	private static DynamicDictionary Deserialize(string json)
	{
		var bytes = Encoding.UTF8.GetBytes(json);
		using var stream = new MemoryStream(bytes);
		return LowLevelRequestResponseSerializer.Instance.Deserialize<DynamicDictionary>(stream);
	}

	// --- Flat primitives ---

	[Fact]
	public void FlatString() => Deserialize("""{"name":"hello"}""").Get<string>("name").Should().Be("hello");

	[Fact]
	public void FlatInt() => Deserialize("""{"count":42}""").Get<int>("count").Should().Be(42);

	[Fact]
	public void FlatLong() => Deserialize("""{"big":9999999999}""").Get<long>("big").Should().Be(9999999999L);

	[Fact]
	public void FlatDouble() => Deserialize("""{"pi":3.14}""").Get<double>("pi").Should().BeApproximately(3.14, 0.001);

	[Fact]
	public void FlatBoolTrue() => Deserialize("""{"flag":true}""").Get<bool>("flag").Should().BeTrue();

	[Fact]
	public void FlatBoolFalse() => Deserialize("""{"flag":false}""").Get<bool>("flag").Should().BeFalse();

	[Fact]
	public void FlatNull() => Deserialize("""{"val":null}""").Get<string>("val").Should().BeNull();

	// --- Nested objects ---

	[Fact]
	public void NestedTwoLevels() =>
		Deserialize("""{"a":{"b":"deep"}}""").Get<string>("a.b").Should().Be("deep");

	[Fact]
	public void NestedThreeLevels() =>
		Deserialize("""{"a":{"b":{"c":99}}}""").Get<int>("a.b.c").Should().Be(99);

	// --- Array properties ---

	[Fact]
	public void ArrayPropertyByNumericIndex() =>
		Deserialize("""{"items":[10,20,30]}""").Get<int>("items.0").Should().Be(10);

	[Fact]
	public void ArrayPropertyByNumericIndexSecond() =>
		Deserialize("""{"items":[10,20,30]}""").Get<int>("items.1").Should().Be(20);

	[Fact]
	public void ArrayOfObjectsByIndex() =>
		Deserialize("""{"hits":[{"name":"a"},{"name":"b"}]}""").Get<string>("hits.0.name").Should().Be("a");

	[Fact]
	public void ArrayOfObjectsByIndexSecond() =>
		Deserialize("""{"hits":[{"name":"a"},{"name":"b"}]}""").Get<string>("hits.1.name").Should().Be("b");

	// --- Bracket index syntax [N] ---

	[Fact]
	public void BracketIndex() =>
		Deserialize("""{"items":[10,20,30]}""").Get<int>("items.[0]").Should().Be(10);

	[Fact]
	public void BracketIndexSecond() =>
		Deserialize("""{"items":[10,20,30]}""").Get<int>("items.[2]").Should().Be(30);

	[Fact]
	public void BracketIndexOnNestedArrayOfObjects() =>
		Deserialize("""{"hits":{"hits":[{"_source":{"name":"first"}},{"_source":{"name":"second"}}]}}""")
			.Get<string>("hits.hits.[0]._source.name").Should().Be("first");

	[Fact]
	public void BracketIndexOnNestedArrayOfObjectsLast() =>
		Deserialize("""{"hits":{"hits":[{"_source":{"name":"first"}},{"_source":{"name":"second"}}]}}""")
			.Get<string>("hits.hits.[1]._source.name").Should().Be("second");

	// --- [first()] and _first_ ---

	[Fact]
	public void BracketFirst() =>
		Deserialize("""{"items":["a","b","c"]}""").Get<string>("items.[first()]").Should().Be("a");

	[Fact]
	public void UnderscoreFirst() =>
		Deserialize("""{"items":["a","b","c"]}""").Get<string>("items._first_").Should().Be("a");

	[Fact]
	public void BracketFirstOnObjects() =>
		Deserialize("""{"hits":{"hits":[{"_source":{"name":"first"}},{"_source":{"name":"second"}}]}}""")
			.Get<string>("hits.hits.[first()]._source.name").Should().Be("first");

	[Fact]
	public void UnderscoreFirstOnObjects() =>
		Deserialize("""{"hits":{"hits":[{"_source":{"name":"first"}},{"_source":{"name":"second"}}]}}""")
			.Get<string>("hits.hits._first_._source.name").Should().Be("first");

	// --- [last()] and _last_ ---

	[Fact]
	public void BracketLast() =>
		Deserialize("""{"items":["a","b","c"]}""").Get<string>("items.[last()]").Should().Be("c");

	[Fact]
	public void UnderscoreLast() =>
		Deserialize("""{"items":["a","b","c"]}""").Get<string>("items._last_").Should().Be("c");

	[Fact]
	public void BracketLastOnObjects() =>
		Deserialize("""{"hits":{"hits":[{"_source":{"name":"first"}},{"_source":{"name":"second"}}]}}""")
			.Get<string>("hits.hits.[last()]._source.name").Should().Be("second");

	[Fact]
	public void UnderscoreLastOnObjects() =>
		Deserialize("""{"hits":{"hits":[{"_source":{"name":"first"}},{"_source":{"name":"second"}}]}}""")
			.Get<string>("hits.hits._last_._source.name").Should().Be("second");

	// --- Deep nesting: objects containing arrays containing objects ---

	[Fact]
	public void DeepNesting() =>
		Deserialize("""{"response":{"data":{"items":[{"sub":{"value":42}}]}}}""")
			.Get<int>("response.data.items.0.sub.value").Should().Be(42);

	[Fact]
	public void DeepNestingWithBrackets() =>
		Deserialize("""{"response":{"data":{"items":[{"sub":{"value":42}}]}}}""")
			.Get<int>("response.data.items.[0].sub.value").Should().Be(42);

	// --- _arbitrary_key_ ---

	[Fact]
	public void ArbitraryKeyTraversesIntoFirstKey() =>
		Deserialize("""{"data":{"some_key":{"value":1}}}""")
			.Get<int>("data._arbitrary_key_.value").Should().Be(1);

	[Fact]
	public void ArbitraryKeyReturnsKeyName() =>
		Deserialize("""{"data":{"first_key":"v1","second_key":"v2"}}""")
			.Get<string>("data._arbitrary_key_").Should().NotBeNull();

	// --- Edge cases ---

	[Fact]
	public void MissingPath() =>
		Deserialize("""{"a":1}""").Get<string>("b").Should().BeNull();

	[Fact]
	public void DeepMissingPath() =>
		Deserialize("""{"a":{"b":1}}""").Get<string>("a.c.d").Should().BeNull();

	[Fact]
	public void NullPath() =>
		Deserialize("""{"a":1}""").Get<string>(null).Should().BeNull();

	[Fact]
	public void EmptyObject() =>
		Deserialize("""{}""").Get<string>("a").Should().BeNull();

	[Fact]
	public void OutOfBoundsIndex() =>
		Deserialize("""{"items":[1]}""").Get<int>("items.99").Should().Be(0);

	[Fact]
	public void OutOfBoundsFirstOnEmpty()
	{
		// _first_ on empty array returns NullValue
		var result = Deserialize("""{"items":[]}""").Get<string>("items._first_");
		result.Should().BeNull();
	}

	[Fact]
	public void OutOfBoundsLastOnEmpty()
	{
		var result = Deserialize("""{"items":[]}""").Get<string>("items._last_");
		result.Should().BeNull();
	}

	// --- Type conversions ---

	[Fact]
	public void IntAsLong() =>
		Deserialize("""{"v":42}""").Get<long>("v").Should().Be(42L);

	[Fact]
	public void DoubleAsFloat() =>
		Deserialize("""{"v":3.14}""").Get<float>("v").Should().BeApproximately(3.14f, 0.001f);

	[Fact]
	public void DateTimeParsing() =>
		Deserialize("""{"d":"2024-01-15T10:30:00Z"}""").Get<DateTime>("d").Should().Be(new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc));

	// --- Root-level array ---

	[Fact]
	public void RootLevelArrayByIndex() =>
		Deserialize("""[{"name":"a"},{"name":"b"}]""").Get<string>("0.name").Should().Be("a");

	[Fact]
	public void RootLevelArraySecondElement() =>
		Deserialize("""[{"name":"a"},{"name":"b"}]""").Get<string>("1.name").Should().Be("b");
}
