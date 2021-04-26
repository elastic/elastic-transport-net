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

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Elastic.Transport.Products.Elasticsearch.Failures
{
	/// <summary> Represents the serialized Elasticsearch java exception that caused a request to fail </summary>
	[DataContract]
	public class Error : ErrorCause
	{
		private static readonly IReadOnlyDictionary<string, string> DefaultHeaders =
			new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(0));

		/// <summary> Additional headers from the request that pertain to the error</summary>
		[DataMember(Name = "headers")]
		[JsonPropertyName("headers")]
		public IReadOnlyDictionary<string, string> Headers { get; set; } = DefaultHeaders;

		/// <summary> The root cause exception </summary>
		[DataMember(Name = "root_cause")]
		[JsonPropertyName("root_cause")]
		public IReadOnlyCollection<ErrorCause> RootCause { get; set; }

		/// <summary> A human readable string representation of the exception returned by Elasticsearch </summary>
		public override string ToString() => CausedBy == null
			? $"Type: {Type} Reason: \"{Reason}\""
			: $"Type: {Type} Reason: \"{Reason}\" CausedBy: \"{CausedBy}\"";
	}
}
