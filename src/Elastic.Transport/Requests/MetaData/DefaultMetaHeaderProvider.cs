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
	/// <inheritdoc cref="MetaHeaderProvider.Producers"/>
	public override MetaHeaderProducer[] Producers { get; }

	/// <summary>
	///
	/// </summary>
	public DefaultMetaHeaderProvider(Type clientType, string serviceIdentifier) =>
		Producers = [new DefaultMetaHeaderProducer(clientType, serviceIdentifier)];

	/// <summary>
	///
	/// </summary>
	public DefaultMetaHeaderProvider(VersionInfo versionInfo, string serviceIdentifier) =>
		Producers = [new DefaultMetaHeaderProducer(versionInfo, serviceIdentifier)];
}

/// <summary>
///
/// </summary>
public sealed class DefaultMetaHeaderProducer : MetaHeaderProducer
{
	private const string MetaHeaderName = "x-elastic-client-meta";

	private readonly MetaDataHeader _asyncMetaDataHeader;
	private readonly MetaDataHeader _syncMetaDataHeader;

	/// <summary>
	///
	/// </summary>
	public DefaultMetaHeaderProducer(Type clientType, string serviceIdentifier)
	{
		var clientVersionInfo = ReflectionVersionInfo.Create(clientType);
		_asyncMetaDataHeader = new MetaDataHeader(clientVersionInfo, serviceIdentifier, true);
		_syncMetaDataHeader = new MetaDataHeader(clientVersionInfo, serviceIdentifier, false);
	}

	/// <summary>
	///
	/// </summary>
	public DefaultMetaHeaderProducer(VersionInfo versionInfo, string serviceIdentifier)
	{
		_asyncMetaDataHeader = new MetaDataHeader(versionInfo, serviceIdentifier, true);
		_syncMetaDataHeader = new MetaDataHeader(versionInfo, serviceIdentifier, false);
	}

	/// <inheritdoc/>
	public override string HeaderName => MetaHeaderName;

	/// <inheritdoc/>
	public override string? ProduceHeaderValue(BoundConfiguration boundConfiguration, bool isAsync)
	{
		try
		{
			if (boundConfiguration.ConnectionSettings.DisableMetaHeader)
				return null;

			var headerValue = isAsync
				? _asyncMetaDataHeader.ToString()
				: _syncMetaDataHeader.ToString();

			// TODO - Cache values against key to avoid allocating a string each time
			if (boundConfiguration.RequestMetaData is not null && boundConfiguration.RequestMetaData.Items.TryGetValue(RequestMetaData.HelperKey, out var helperSuffix))
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
