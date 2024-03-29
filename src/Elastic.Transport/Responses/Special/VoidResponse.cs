// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

namespace Elastic.Transport;

/// <summary>
/// A special response that omits reading the response from the server after reading the headers.
/// </summary>
public sealed class VoidResponse : TransportResponse<VoidResponse.VoidBody>
{
	/// <inheritdoc cref="VoidResponse"/>
	public VoidResponse() => Body = new VoidBody();

	/// <summary>
	/// A static <see cref="VoidResponse"/> instance that can be reused.
	/// </summary>
	public static VoidResponse Default { get; } = new VoidResponse();

	/// <summary>
	/// A class that represents the absence of having read the servers response to completion.
	/// </summary>
	public class VoidBody { }
}
