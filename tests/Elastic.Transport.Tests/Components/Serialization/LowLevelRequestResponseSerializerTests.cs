// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Elastic.Transport.Tests.Components.Serialization;

public class LowLevelRequestResponseSerializerTests : SerializerTestBase
{
	[Fact]
	public void SerializesException()
	{
		// NOTE: Any changes to this file, may change the assertion since we validate the full JSON which
		// includes the stack trace line numbers. As we don't foresee this changing, this should be okay.

		const string urlValue = "https://www.elastic.co";
		const string messageValue = "Testing";

		Exception e;

		try
		{
			throw new CustomException(messageValue) { HelpLink = urlValue };
		}
		catch (Exception ex)
		{
			e = ex;
		}

		using var stream = SerializeToStream(e);

		var jsonDocument = JsonDocument.Parse(stream);

		_ = jsonDocument.RootElement.EnumerateArray().Should().HaveCount(1);
		var exception = jsonDocument.RootElement.EnumerateArray().First();

		_ = exception.TryGetProperty("Depth", out var depth).Should().BeTrue();
		_ = depth.ValueKind.Should().Be(JsonValueKind.Number);
		_ = depth.GetInt32().Should().Be(0);

		_ = exception.TryGetProperty("ClassName", out var className).Should().BeTrue();
		_ = className.ValueKind.Should().Be(JsonValueKind.String);
		_ = className.GetString().Should().Be("Elastic.Transport.Tests.Components.Serialization.CustomException");

		_ = exception.TryGetProperty("Message", out var message).Should().BeTrue();
		_ = message.ValueKind.Should().Be(JsonValueKind.String);
		_ = message.GetString().Should().Be(messageValue);

		_ = exception.TryGetProperty("Source", out var source).Should().BeTrue();
		_ = source.ValueKind.Should().Be(JsonValueKind.String);
		_ = source.GetString().Should().Be("Elastic.Transport.Tests");

		var windowsPath = "Components\\Serialization\\LowLevelRequestResponseSerializerTests.cs";
		var path = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
			? windowsPath
			: windowsPath.Replace('\\', '/');

		_ = exception.TryGetProperty("StackTraceString", out var stackTrace).Should().BeTrue();
		_ = stackTrace.ValueKind.Should().Be(JsonValueKind.String);
		var stackTraceString = stackTrace.GetString();
		_ = stackTraceString.Should()
			.Contain("at Elastic.Transport.Tests.Components.Serialization.LowLevelRequestResponseSerializerTests")
			.And.Contain(path, stackTraceString);

		_ = exception.TryGetProperty("HResult", out var hResult).Should().BeTrue();
		_ = hResult.ValueKind.Should().Be(JsonValueKind.Number);
		_ = hResult.GetInt32().Should().Be(-2146233088);

		_ = exception.TryGetProperty("HelpURL", out var helpUrl).Should().BeTrue();
		_ = helpUrl.ValueKind.Should().Be(JsonValueKind.String);
		_ = helpUrl.GetString().Should().Be(urlValue);
	}
}

internal sealed class CustomException(string message) : Exception(message);
