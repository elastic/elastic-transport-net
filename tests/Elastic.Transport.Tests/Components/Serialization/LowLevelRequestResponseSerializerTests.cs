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

		jsonDocument.RootElement.EnumerateArray().Should().HaveCount(1);
		var exception = jsonDocument.RootElement.EnumerateArray().First();

		exception.TryGetProperty("Depth", out var depth).Should().BeTrue();
		depth.ValueKind.Should().Be(JsonValueKind.Number);
		depth.GetInt32().Should().Be(0);

		exception.TryGetProperty("ClassName", out var className).Should().BeTrue();
		className.ValueKind.Should().Be(JsonValueKind.String);
		className.GetString().Should().Be("Elastic.Transport.Tests.Components.Serialization.CustomException");

		exception.TryGetProperty("Message", out var message).Should().BeTrue();
		message.ValueKind.Should().Be(JsonValueKind.String);
		message.GetString().Should().Be(messageValue);

		exception.TryGetProperty("Source", out var source).Should().BeTrue();
		source.ValueKind.Should().Be(JsonValueKind.String);
		source.GetString().Should().Be("Elastic.Transport.Tests");

		var windowsPath = "Components\\Serialization\\LowLevelRequestResponseSerializerTests.cs";
		var path = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
			? windowsPath
			: windowsPath.Replace('\\', '/');

		exception.TryGetProperty("StackTraceString", out var stackTrace).Should().BeTrue();
		stackTrace.ValueKind.Should().Be(JsonValueKind.String);
		var stackTraceString = stackTrace.GetString();
		stackTraceString.Should()
			.Contain("at Elastic.Transport.Tests.Components.Serialization.LowLevelRequestResponseSerializerTests")
			.And.Contain(path, stackTraceString);

		exception.TryGetProperty("HResult", out var hResult).Should().BeTrue();
		hResult.ValueKind.Should().Be(JsonValueKind.Number);
		hResult.GetInt32().Should().Be(-2146233088);

		exception.TryGetProperty("HelpURL", out var helpUrl).Should().BeTrue();
		helpUrl.ValueKind.Should().Be(JsonValueKind.String);
		helpUrl.GetString().Should().Be(urlValue);
	}
}

internal sealed class CustomException(string message) : Exception(message);
