// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;

namespace Elastic.Transport.Products.Elasticsearch;

internal sealed class NodeInfo
{
	public string? build_hash { get; set; }
	public string? host { get; set; }
	public NodeInfoHttp? http { get; set; }
	public string? ip { get; set; }
	public string? name { get; set; }
	public IList<string>? roles { get; set; }
	public IDictionary<string, object>? settings { get; set; }
	public string? transport_address { get; set; }
	public string? version { get; set; }
	internal bool HoldsData => roles?.Contains("data") ?? false;

	internal bool HttpEnabled
	{
		get
		{
			if (settings != null && settings.TryGetValue("http.enabled", out var httpEnabled))
			{
				if (httpEnabled is JsonElement e)
					return e.GetBoolean();
				return Convert.ToBoolean(httpEnabled, CultureInfo.InvariantCulture);
			}

			return http != null;
		}
	}

	internal bool IngestEnabled => roles?.Contains("ingest") ?? false;

	internal bool MasterEligible => roles?.Contains("master") ?? false;
}
