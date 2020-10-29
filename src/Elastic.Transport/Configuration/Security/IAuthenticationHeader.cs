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
