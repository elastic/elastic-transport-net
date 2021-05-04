// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Elastic.Transport.Products.Elasticsearch.Failures
{
	/// <summary> Represents a failure that occurred on a shard involved in the request </summary>
	[DataContract]
	public class ShardFailure
	{
		/// <summary> This index this shard belongs to </summary>
		[DataMember(Name = "index")]
		[JsonPropertyName("index")]
		public string Index { get; set; }

		/// <summary> The node the shard is currently allocated on</summary>
		[DataMember(Name = "node")]
		[JsonPropertyName("node")]
		public string Node { get; set; }

		/// <summary>
		/// The java exception that caused the shard to fail
		/// </summary>
		[DataMember(Name = "reason")]
		[JsonPropertyName("reason")]
		public ErrorCause Reason { get; set; }

		/// <summary> The shard number that failed </summary>
		[DataMember(Name = "shard")]
		[JsonPropertyName("shard")]
		public int? Shard { get; set; }

		/// <summary> The status of the shard when the exception occured</summary>
		[DataMember(Name = "status")]
		[JsonPropertyName("status")]
		public string Status { get; set; }
	}
}
