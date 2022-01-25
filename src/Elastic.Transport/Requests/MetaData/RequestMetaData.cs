// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Collections.Generic;
using Elastic.Transport.Extensions;

namespace Elastic.Transport
{
	/// <summary>
	/// Holds meta data about a client request.
	/// </summary>
	public sealed class RequestMetaData
	{
		/// <summary>
		/// Reserved key for a meta data entry which identifies the helper which produced the request.
		/// </summary>
		internal const string HelperKey = "helper";

		private Dictionary<string, string> _metaDataItems;

		internal bool TryAddMetaData(string key, string value)
		{
			_metaDataItems ??= new Dictionary<string, string>();

#if NETSTANDARD2_1
			return _metaDataItems.TryAdd(key, value);
#else
			if (_metaDataItems.ContainsKey(key))
				return false;

			_metaDataItems.Add(key, value);
			return true;
#endif
		}

		/// <summary>
		/// 
		/// </summary>
		public IReadOnlyDictionary<string, string> Items => _metaDataItems ?? EmptyReadOnly<string, string>.Dictionary;
	}
}
