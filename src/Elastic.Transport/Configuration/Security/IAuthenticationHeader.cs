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

namespace Elastic.Transport
{
	/// <summary>
	/// An implementation of <see cref="IAuthenticationHeader"/> describing what http header to use to authenticate with the product.
	/// <para><see cref="BasicAuthentication"/> for basic authentication</para>
	/// <para><see cref="ApiKey"/> for simple secret token</para>
	/// <para><see cref="Base64ApiKey"/> for Elastic Cloud style encoded api keys</para>
	/// </summary>
	public interface IAuthenticationHeader : IDisposable
	{
		/// <summary> The header to use to authenticate the request </summary>
		public string Header { get; }

		/// <summary>
		/// If this instance is valid return the header name and value to use for authentication
		/// </summary>
		bool TryGetHeader(out string value);
	}
}
