// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

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
