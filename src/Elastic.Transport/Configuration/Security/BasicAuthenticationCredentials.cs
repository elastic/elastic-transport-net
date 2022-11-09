// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Text;

namespace Elastic.Transport;

/// <summary>
/// Credentials for Basic Authentication.
/// </summary>
public sealed class BasicAuthentication : AuthorizationHeader
{
	private readonly string _base64String;

	/// <summary> The default http header used for basic authentication </summary>
	public static string BasicAuthenticationScheme { get; } = "Basic";

	/// <inheritdoc cref="BasicAuthentication"/>
	public BasicAuthentication(string username, string password) =>
		_base64String = GetBase64String($"{username}:{password}");

	/// <inheritdoc cref="AuthorizationHeader.AuthScheme"/>
	public override string AuthScheme { get; } = BasicAuthenticationScheme;

	/// <inheritdoc cref="AuthorizationHeader.TryGetAuthorizationParameters(out string)"/>
	public override bool TryGetAuthorizationParameters(out string value)
	{
		value = _base64String;
		return true;
	}

	/// <summary> Get Base64 representation for string </summary>
	public static string GetBase64String(string header) =>
		Convert.ToBase64String(Encoding.UTF8.GetBytes(header));
}
