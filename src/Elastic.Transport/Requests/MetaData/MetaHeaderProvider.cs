// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

namespace Elastic.Transport
{
	/// <summary>
	/// TODO
	/// </summary>
	public abstract class MetaHeaderProvider
	{
		/// <summary>
		/// 
		/// </summary>
		public abstract string HeaderName { get; }

		/// <summary>
		/// TODO
		/// </summary>
		/// <param name="requestData"></param>
		/// <returns></returns>
		public abstract string ProduceHeaderValue(RequestData requestData);
	}
}
