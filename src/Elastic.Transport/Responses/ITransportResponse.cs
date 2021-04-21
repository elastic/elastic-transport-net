// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

namespace Elastic.Transport
{
	/// <summary>
	/// The minimum interface which custom responses should implement when providing a response type
	/// to the low level client.
	/// </summary>
	public interface ITransportResponse
	{
		/// <summary>
		/// <see cref="ITransport{TConnectionSettings}"/> sets the <see cref="IApiCallDetails" /> diagnostic information about the request and response.
		/// </summary>
		IApiCallDetails ApiCall { get; set; }
	}
}
