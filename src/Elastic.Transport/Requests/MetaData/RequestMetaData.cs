// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Collections.Generic;

namespace Elastic.Transport;

/// <summary>
/// Holds meta data about a client request.
/// </summary>
public sealed class RequestMetaData
{
	/// <summary>
	/// Reserved key for a meta data entry which identifies the helper which produced the request.
	/// </summary>
	internal const string HelperKey = "helper";

	private Dictionary<string, string>? _metaDataItems;

	internal bool TryAddMetaData(string key, string value)
	{
		_metaDataItems ??= [];

#if NETSTANDARD2_1 || NET6_0_OR_GREATER
		return _metaDataItems.TryAdd(key, value);
#else
		if (_metaDataItems.ContainsKey(key))
			return false;

		_metaDataItems.Add(key, value);
		return true;
#endif
	}

	/// <summary> Retrieves a read-only dictionary of metadata items associated with a client request.</summary>
	public IReadOnlyDictionary<string, string>? Items => _metaDataItems;
}
