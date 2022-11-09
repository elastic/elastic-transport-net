// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

namespace Elastic.Transport;

/// <summary>
/// The HTTP authorization request header used to provide credentials that authenticate a user agent with a server.
/// </summary>
public abstract class AuthorizationHeader
{
	/// <summary>
	/// The authentication scheme that defines how the credentials are encoded.
	/// </summary>
	public abstract string AuthScheme { get; }

	/// <summary>
	/// If this instance is valid, returns the authorization parameters to include in the header.
	/// </summary>
	public abstract bool TryGetAuthorizationParameters(out string value);
}
