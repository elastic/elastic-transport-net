// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.IO;
using System.Text;

namespace Elastic.Transport.Extensions;

/// <summary>
/// Streams over data we already hold, for something that only reads them (a deserializer, a response replay).
/// </summary>
/// <remarks>
/// On .NET 11 these are <c>ReadOnlyMemoryStream</c> and <c>StringStream</c>: they read the bytes (or encode the string as
/// it is read) in place, where a <see cref="MemoryStreamFactory"/> stream first copies them, and a pooled factory copies them
/// into pooled blocks. Everywhere else the factory is used exactly as before, so a custom factory still sees these calls.
/// </remarks>
internal static class ReadOnlyStreams
{
	/// <summary>A stream that reads <paramref name="bytes"/>.</summary>
	public static Stream Over(MemoryStreamFactory factory, byte[] bytes) =>
#if NET11_0_OR_GREATER
		new ReadOnlyMemoryStream(bytes);
#else
		factory.Create(bytes);
#endif

	/// <summary>A stream that reads <paramref name="text"/> as UTF-8.</summary>
	public static Stream OverUtf8(MemoryStreamFactory factory, string text) =>
#if NET11_0_OR_GREATER
		new StringStream(text, Encoding.UTF8);
#else
		factory.Create(Encoding.UTF8.GetBytes(text));
#endif

	/// <summary>A stream that reads <paramref name="text"/> as UTF-8.</summary>
	public static Stream OverUtf8(MemoryStreamFactory factory, ReadOnlySpan<char> text) =>
#if NET11_0_OR_GREATER
		new StringStream(text.ToString(), Encoding.UTF8);
#else
		factory.Create(Encoding.UTF8.GetBytes(text.ToArray()));
#endif
}
