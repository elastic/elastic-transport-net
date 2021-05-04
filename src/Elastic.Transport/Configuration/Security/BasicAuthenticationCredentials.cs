// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Security;
using System.Text;

namespace Elastic.Transport
{
	/// <summary> Credentials for Basic Authentication </summary>
	public class BasicAuthentication : IAuthenticationHeader
	{
		/// <inheritdoc cref="BasicAuthentication"/>
		public BasicAuthentication(string username, string password)
		{
			Username = username;
			_cachedHeaderValue = GetBase64String($"{Username}:{password}");
		}

		/// <inheritdoc cref="BasicAuthentication"/>
		public BasicAuthentication(string username, SecureString password)
		{
			Username = username;
			Password = password;
		}

		private readonly string _cachedHeaderValue;

		/// <summary> The password with which to authenticate </summary>
		private SecureString Password { get; }

		/// <summary> The username with which to authenticate </summary>
		private string Username { get; }

		/// <inheritdoc cref="IDisposable.Dispose "/>
		public void Dispose() => Password?.Dispose();

		/// <inheritdoc cref="IAuthenticationHeader.Header"/>
		public string Header { get; } = Base64Header;

		/// <summary> The default http header used for basic authentication </summary>
		public static string Base64Header { get; } = "Basic";

		/// <inheritdoc cref="IAuthenticationHeader.TryGetHeader"/>
		public bool TryGetHeader(out string value)
		{
			value = _cachedHeaderValue ?? GetBase64String($"{Username}:{Password.CreateString()}");
			return true;
		}

		/// <summary> Get Base64 representation for string </summary>
		public static string GetBase64String(string header) =>
			Convert.ToBase64String(Encoding.UTF8.GetBytes(header));
	}
}
