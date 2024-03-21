// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using VerifyXunit;

namespace Elastic.Transport.Tests.Components.Serialization;

public abstract class VerifySerializerTestBase : VerifyBase
{
	public VerifySerializerTestBase() : base() { }

	private static readonly Serializer RequestResponseSerializer = LowLevelRequestResponseSerializer.Instance;

	/// <summary>
	/// Serialises the <paramref name="data"/> using the sync and async request/response serializer methods, comparing the results.
	/// </summary>
	/// <returns>
	/// The JSON as a string for further comparisons and assertions.
	/// </returns>
	protected static async Task<string> SerializeAndGetJsonStringAsync<T>(T data)
	{
		var stream = new MemoryStream();
		await RequestResponseSerializer.SerializeAsync(data, stream);
		stream.Position = 0;
		var reader = new StreamReader(stream);
		var asyncJsonString = await reader.ReadToEndAsync();

		stream.SetLength(0);
		RequestResponseSerializer.Serialize(data, stream);
		stream.Position = 0;
		reader = new StreamReader(stream);
		var syncJsonString = await reader.ReadToEndAsync();

		syncJsonString.Should().Be(asyncJsonString);

		return asyncJsonString;
	}
}
