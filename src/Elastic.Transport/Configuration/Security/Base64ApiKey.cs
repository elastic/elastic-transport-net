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
	public class Base64ApiKey : IAuthenticationHeader
	{
		/// <inheritdoc cref="Base64ApiKey"/>
		public Base64ApiKey(string id, SecureString apiKey) =>
			Value = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{id}:{apiKey.CreateString()}")).CreateSecureString();

		/// <inheritdoc cref="Base64ApiKey"/>
		public Base64ApiKey(string id, string apiKey) =>
			Value = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{id}:{apiKey}")).CreateSecureString();

		/// <inheritdoc cref="Base64ApiKey"/>
		public Base64ApiKey(string base64EncodedApiKey) =>
			Value = base64EncodedApiKey.CreateSecureString();

		/// <inheritdoc cref="Base64ApiKey"/>
		public Base64ApiKey(SecureString base64EncodedApiKey) =>
			Value = base64EncodedApiKey;

		private SecureString Value { get; }

		/// <inheritdoc cref="IDisposable.Dispose "/>
		public void Dispose() => Value?.Dispose();

		/// <inheritdoc cref="IAuthenticationHeader.Header"/>
		public string Header { get; } = "ApiKey";

		/// <inheritdoc cref="IAuthenticationHeader.TryGetHeader"/>
		public bool TryGetHeader(out string value)
		{
			value = Value.CreateString();
			return true;
		}
	}
}
