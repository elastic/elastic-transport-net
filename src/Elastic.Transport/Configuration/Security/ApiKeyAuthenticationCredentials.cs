// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Security;
using System.Text;

namespace Elastic.Transport
{
	/// <summary>
	/// Credentials for Api Key Authentication
	/// </summary>
	public class ApiKeyAuthenticationCredentials : IDisposable
	{
		/// <inheritdoc cref="ApiKeyAuthenticationCredentials"/>
		public ApiKeyAuthenticationCredentials(string id, SecureString apiKey) =>
			Base64EncodedApiKey = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{id}:{apiKey.CreateString()}")).CreateSecureString();

		/// <inheritdoc cref="ApiKeyAuthenticationCredentials"/>
		public ApiKeyAuthenticationCredentials(string id, string apiKey) =>
			Base64EncodedApiKey = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{id}:{apiKey}")).CreateSecureString();

		/// <inheritdoc cref="ApiKeyAuthenticationCredentials"/>
		public ApiKeyAuthenticationCredentials(string base64EncodedApiKey) =>
			Base64EncodedApiKey = base64EncodedApiKey.CreateSecureString();

		/// <inheritdoc cref="ApiKeyAuthenticationCredentials"/>
		public ApiKeyAuthenticationCredentials(SecureString base64EncodedApiKey) =>
			Base64EncodedApiKey = base64EncodedApiKey;

		/// <summary>
		/// The Base64 encoded api key with which to authenticate
		/// Take the form, id:api_key, which is then base 64 encoded
		/// </summary>
		public SecureString Base64EncodedApiKey { get; }

		/// <inheritdoc cref="IDisposable.Dispose "/>
		public void Dispose() => Base64EncodedApiKey?.Dispose();
	}
}
