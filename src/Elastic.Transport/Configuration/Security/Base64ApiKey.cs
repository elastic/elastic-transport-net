// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Text;

namespace Elastic.Transport;

/// <summary>
/// Credentials for Api Key Authentication
/// </summary>
public class Base64ApiKey : ApiKey
{
	/// <inheritdoc cref="Base64ApiKey"/>
	public Base64ApiKey(string id, string apiKey) :
		base(Convert.ToBase64String(Encoding.UTF8.GetBytes($"{id}:{apiKey}")))
	{ }

	/// <inheritdoc cref="Base64ApiKey"/>
	public Base64ApiKey(string base64EncodedApiKey) : base(base64EncodedApiKey) { }
}
