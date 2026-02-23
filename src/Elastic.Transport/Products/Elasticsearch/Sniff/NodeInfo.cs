// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Elastic.Transport.Products.Elasticsearch;

internal sealed class NodeInfo
{
	[JsonPropertyName("build_hash")]
	public string? BuildHash { get; set; }

	[JsonPropertyName("host")]
	public string? Host { get; set; }

	[JsonPropertyName("http")]
	public NodeInfoHttp? Http { get; set; }

	[JsonPropertyName("ip")]
	public string? Ip { get; set; }

	[JsonPropertyName("name")]
	public string? Name { get; set; }

	[JsonPropertyName("roles")]
	public IList<string>? Roles { get; set; }

	[JsonPropertyName("settings")]
	public IDictionary<string, object>? Settings { get; set; }

	[JsonPropertyName("transport_address")]
	public string? TransportAddress { get; set; }

	[JsonPropertyName("version")]
	public string? Version { get; set; }

	internal bool HoldsData => Roles?.Contains("data") ?? false;

	internal bool HttpEnabled
	{
		get
		{
			if (Settings != null && Settings.TryGetValue("http.enabled", out var httpEnabled))
			{
				if (httpEnabled is JsonElement e)
					return e.GetBoolean();
				return Convert.ToBoolean(httpEnabled, CultureInfo.InvariantCulture);
			}

			return Http != null;
		}
	}

	internal bool IngestEnabled => Roles?.Contains("ingest") ?? false;

	internal bool MasterEligible => Roles?.Contains("master") ?? false;
}
