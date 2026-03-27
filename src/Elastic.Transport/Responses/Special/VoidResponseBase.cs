// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

namespace Elastic.Transport;

/// <summary>
/// Base class for responses that omit reading the response body from the server.
/// </summary>
public abstract class VoidResponseBase : TransportResponse<VoidResponseBase.VoidBody>
{
	/// <inheritdoc cref="VoidResponseBase"/>
	protected VoidResponseBase() => Body = new VoidBody();

	/// <summary>
	/// A class that represents the absence of having read the server's response to completion.
	/// </summary>
	public class VoidBody { }
}
