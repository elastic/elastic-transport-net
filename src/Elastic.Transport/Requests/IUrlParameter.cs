// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

namespace Elastic.Transport
{
	/// <summary> Implementers define an object that can be serialized as a query string parameter </summary>
	public interface IUrlParameter
	{
		/// <summary> Get the a string representation using <paramref name="settings"/> </summary>
		string GetString(ITransportConfiguration settings);
	}
}
