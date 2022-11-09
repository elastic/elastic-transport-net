// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Elastic.Transport.Products.Elasticsearch;

/// <summary> Represents a failure that occurred on a shard involved in the request </summary>
[DataContract]
public sealed class ShardFailure
{
	/// <summary> This index this shard belongs to </summary>
	[JsonPropertyName("index")]
	public string Index { get; set; }

	/// <summary> The node the shard is currently allocated on</summary>
	[JsonPropertyName("node")]
	public string Node { get; set; }

	/// <summary>
	/// The java exception that caused the shard to fail
	/// </summary>
	[JsonPropertyName("reason")]
	public ErrorCause Reason { get; set; }

	/// <summary> The shard number that failed </summary>
	[JsonPropertyName("shard")]
	[JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
	public int? Shard { get; set; }

	/// <summary> The status of the shard when the exception occured</summary>
	[JsonPropertyName("status")]
	public string Status { get; set; }
}
