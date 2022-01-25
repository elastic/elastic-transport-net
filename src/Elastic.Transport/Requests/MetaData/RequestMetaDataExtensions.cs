// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;

namespace Elastic.Transport
{
	/// <summary>
	/// 
	/// </summary>
	public static class RequestMetaDataExtensions
	{
		/// <summary>
		/// 
		/// </summary>
		/// <param name="metaData"></param>
		/// <param name="helperValue"></param>
		/// <exception cref="InvalidOperationException"></exception>
		public static void AddHelper(this RequestMetaData metaData, string helperValue)
		{
			if (!metaData.TryAddMetaData(RequestMetaData.HelperKey, helperValue))
				throw new InvalidOperationException("A helper value has already been added.");
		}
	}

}
