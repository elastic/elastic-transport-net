// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;

namespace Elastic.Transport;

/// <summary>
/// 
/// </summary>
public sealed class DefaultMetaHeaderProvider : MetaHeaderProvider
{
	private const string MetaHeaderName = "x-elastic-client-meta";

	private readonly MetaDataHeader _asyncMetaDataHeader;
	private readonly MetaDataHeader _syncMetaDataHeader;

	/// <summary>
	/// 
	/// </summary>
	public DefaultMetaHeaderProvider(Type clientType, string serviceIdentifier)
	{
		var clientVersionInfo = ReflectionVersionInfo.Create(clientType);
		_asyncMetaDataHeader = new MetaDataHeader(clientVersionInfo, serviceIdentifier, true);
		_syncMetaDataHeader = new MetaDataHeader(clientVersionInfo, serviceIdentifier, false);
	}

	/// <summary>
	/// 
	/// </summary>
	internal DefaultMetaHeaderProvider(ReflectionVersionInfo reflectionVersionInfo, string serviceIdentifier)
	{
		_asyncMetaDataHeader = new MetaDataHeader(reflectionVersionInfo, serviceIdentifier, true);
		_syncMetaDataHeader = new MetaDataHeader(reflectionVersionInfo, serviceIdentifier, false);
	}

	/// <summary>
	/// 
	/// </summary>
	public override string HeaderName => MetaHeaderName;

	/// <summary>
	/// 
	/// </summary>
	/// <param name="requestData"></param>
	/// <returns></returns>
	public override string ProduceHeaderValue(RequestData requestData)
	{
		try
		{
			if (requestData.ConnectionSettings.DisableMetaHeader)
				return null;

			var headerValue = requestData.IsAsync
				? _asyncMetaDataHeader.ToString()
				: _syncMetaDataHeader.ToString();

			// TODO - Cache values against key to avoid allocating a string each time
			if (requestData.RequestMetaData.TryGetValue(RequestMetaData.HelperKey, out var helperSuffix))
				headerValue = $"{headerValue},h={helperSuffix}";

			return headerValue;
		}
		catch
		{
			// Don't fail the application just because we cannot create this optional header
		}

		return string.Empty;
	}
}
