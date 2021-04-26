/*
 * Licensed to Elasticsearch B.V. under one or more contributor
 * license agreements. See the NOTICE file distributed with
 * this work for additional information regarding copyright
 * ownership. Elasticsearch B.V. licenses this file to you under
 * the Apache License, Version 2.0 (the "License"); you may
 * not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *    http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing,
 * software distributed under the License is distributed on an
 * "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
 * KIND, either express or implied.  See the License for the
 * specific language governing permissions and limitations
 * under the License.
 */

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
