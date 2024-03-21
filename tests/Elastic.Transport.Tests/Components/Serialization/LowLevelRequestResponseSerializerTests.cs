// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Threading.Tasks;
using VerifyXunit;
using Xunit;

namespace Elastic.Transport.Tests.Components.Serialization;

public class LowLevelRequestResponseSerializerTests : VerifySerializerTestBase
{
	[Fact]
	public async Task SerializesException()
	{
		// NOTE: Any changes to this file, may change the assertion since we validate the full JSON which
		// includes the stack trace line numbers. As we don't foresee this changing, this should be okay.

		const string url = "https://www.elastic.co";

		Exception e;

		try
		{
			throw new CustomException("Testing") { HelpLink = url };
		}
		catch (Exception ex)
		{
			e = ex;
		}

		var json = await SerializeAndGetJsonStringAsync(e);

#if NET481
		var version = "net481";
#elif NET8_0
		var version = "net80";
#else
		var version = "unspecified_version";
#endif

		await Verifier.VerifyJson(json).UseFileName($"LowLevelRequestResponseSerializerTests.SerializesException_{version}");
	}
}

internal class CustomException(string message) : Exception(message)
{ }
