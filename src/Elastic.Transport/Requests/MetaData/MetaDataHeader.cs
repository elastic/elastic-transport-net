// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Text;

namespace Elastic.Transport;

/// <summary>
/// 
/// </summary>
public sealed class MetaDataHeader
{
	private const char _separator = ',';

	private readonly string _headerValue;

	/// <summary>
	/// 
	/// </summary>
	/// <param name="version"></param>
	/// <param name="serviceIdentifier"></param>
	/// <param name="isAsync"></param>
	public MetaDataHeader(VersionInfo version, string serviceIdentifier, bool isAsync)
	{
		if (serviceIdentifier != "et")
			TransportVersion = ReflectionVersionInfo.Create<ITransport>().ToString();
		
		ClientVersion = version.ToString();
		RuntimeVersion = new RuntimeVersionInfo().ToString();
		ServiceIdentifier = serviceIdentifier;

		// This code is expected to be called infrequently so we're not concerned with over optimising this

		var builder = new StringBuilder(64)
			.Append(serviceIdentifier).Append('=').Append(ClientVersion).Append(_separator)
			.Append("a=").Append(isAsync ? '1' : '0').Append(_separator)
			.Append("net=").Append(RuntimeVersion).Append(_separator)
			.Append(_httpClientIdentifier).Append('=').Append(RuntimeVersion);

		if (!string.IsNullOrEmpty(TransportVersion))
			builder.Append(_separator).Append("t=").Append(TransportVersion);

		_headerValue = builder.ToString();
	}

	private static readonly string _httpClientIdentifier =
#if !NETFRAMEWORK
		ConnectionInfo.UsingCurlHandler ? "cu" : "so";
#else
		"wr";
#endif

	/// <summary>
	/// 
	/// </summary>
	public string ServiceIdentifier { get; private set; }

	/// <summary>
	/// 
	/// </summary>
	public string ClientVersion { get; private set; }

	/// <summary>
	/// 
	/// </summary>
	public string TransportVersion { get; private set; }

	/// <summary>
	/// 
	/// </summary>
	public string RuntimeVersion { get; private set; }

	/// <summary>
	/// 
	/// </summary>
	/// <returns></returns>
	public override string ToString() => _headerValue;
}
