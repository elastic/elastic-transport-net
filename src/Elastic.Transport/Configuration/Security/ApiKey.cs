/*
 * Licensed to Elasticsearch B.V. under one or more contributor
 * license agreements. See the NOTICE file distributed with
 * this work for additional information regarding copyright
 * ownership. Elasticsearch B.V. licenses this file to you under
 * the Apache License, Version 2.0 (the "License"); you may
 * not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *    http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing,
 * software distributed under the License is distributed on an
 * "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
 * KIND, either express or implied.  See the License for the
 * specific language governing permissions and limitations
 * under the License.
 */

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
