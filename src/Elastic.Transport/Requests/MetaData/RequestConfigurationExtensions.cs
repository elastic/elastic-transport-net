// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;

namespace Elastic.Transport
{
	/// <summary>
	/// 
	/// </summary>
	public static class RequestConfigurationExtensions
	{
		/// <summary>
		/// 
		/// </summary>
		/// <param name="requestConfiguration"></param>
		/// <param name="requestMetaData"></param>
		/// <exception cref="ArgumentNullException"></exception>
		public static void SetRequestMetaData(this IRequestConfiguration requestConfiguration, RequestMetaData requestMetaData)
		{
			if (requestConfiguration is null)
				throw new ArgumentNullException(nameof(requestConfiguration));

			if (requestMetaData is null)
				throw new ArgumentNullException(nameof(requestMetaData));

			requestConfiguration.RequestMetaData = requestMetaData;
		}
	}

}
