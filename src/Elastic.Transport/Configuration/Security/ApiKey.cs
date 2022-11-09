// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

namespace Elastic.Transport;

/// <summary>
/// Credentials for Api Key Authentication
/// </summary>
public sealed class ApiKey : AuthorizationHeader
{
	private readonly string _apiKey;

	/// <inheritdoc cref="Base64ApiKey"/>
	public ApiKey(string apiKey) => _apiKey = apiKey;

	/// <inheritdoc cref="AuthorizationHeader.AuthScheme"/>
	public override string AuthScheme { get; } = "ApiKey";

	/// <inheritdoc cref="AuthorizationHeader.TryGetAuthorizationParameters(out string)"/>
	public override bool TryGetAuthorizationParameters(out string value)
	{
		value = _apiKey;
		return true;
	}
}
