// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Text;

namespace Elastic.Transport;

/// <summary>
/// Credentials for Api Key Authentication
/// </summary>
public sealed class Base64ApiKey : AuthorizationHeader
{
	private readonly string _base64String;

	/// <inheritdoc cref="Base64ApiKey"/>
	public Base64ApiKey(string id, string apiKey) =>
		_base64String = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{id}:{apiKey}"));

	/// <inheritdoc cref="Base64ApiKey"/>
	public Base64ApiKey(string base64EncodedApiKey) =>
		_base64String = base64EncodedApiKey;

	/// <inheritdoc cref="AuthorizationHeader.AuthScheme"/>
	public override string AuthScheme { get; } = "ApiKey";

	/// <inheritdoc cref="AuthorizationHeader.TryGetAuthorizationParameters(out string)"/>
	public override bool TryGetAuthorizationParameters(out string value)
	{
		value = _base64String;
		return true;
	}
}
