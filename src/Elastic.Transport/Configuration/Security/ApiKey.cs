// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Security;

namespace Elastic.Transport
{
	/// <summary>
	/// Credentials for Api Key Authentication
	/// </summary>
	public class ApiKey : IAuthenticationHeader
	{
		/// <inheritdoc cref="Base64ApiKey"/>
		public ApiKey(string apiKey) => Value = apiKey.CreateSecureString();

		/// <inheritdoc cref="Base64ApiKey"/>
		public ApiKey(SecureString apiKey) => Value = apiKey;

		private SecureString Value { get; }

		/// <inheritdoc cref="IAuthenticationHeader.Header"/>
		public virtual string Header { get; } = "Bearer";

		/// <inheritdoc cref="IAuthenticationHeader.TryGetHeader"/>
		public bool TryGetHeader(out string value)
		{
			value = Value.CreateString();
			return true;
		}

		/// <inheritdoc cref="IDisposable.Dispose "/>
		public void Dispose() => Value?.Dispose();

	}
}
