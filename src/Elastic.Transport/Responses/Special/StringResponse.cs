// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

namespace Elastic.Transport
{
	/// <summary>
	/// A response that exposes the response <see cref="TransportResponseBase{T}.Body"/> as <see cref="string"/>.
	/// </summary>
	public sealed class StringResponse : TransportResponseBase<string>
	{
		/// <inheritdoc cref="StringResponse"/>
		public StringResponse() => Body = string.Empty;

		/// <inheritdoc cref="StringResponse"/>
		public StringResponse(string body) => Body = body;
	}
}
