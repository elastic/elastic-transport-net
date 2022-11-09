// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

namespace Elastic.Transport;

/// <summary>
/// Base class for types representing client specific errors. This may be provided by clients to be used for deserialisation of the HTTP body for non-success status codes.
/// </summary>
public abstract class ErrorResponse
{
	internal ErrorResponse() { }

	/// <summary>
	/// May be called by transport to establish whether the instance represents a valid, complete error.
	/// <para>This may not always be the case if the error is partially deserialised on the response.</para>
	/// </summary>
	public abstract bool HasError();
}
