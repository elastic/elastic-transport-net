// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Elastic.Transport.Products.Elasticsearch;

/// <summary> Represents an Elasticsearch server exception. </summary>
[DataContract]
[JsonConverter(typeof(ErrorCauseConverter))]
public class ErrorCause
{
	//private static readonly IReadOnlyCollection<string> DefaultCollection =
	//	new ReadOnlyCollection<string>(new string[0]);

	private static readonly IReadOnlyDictionary<string, object> DefaultDictionary =
		new ReadOnlyDictionary<string, object>(new Dictionary<string, object>());

	//private static readonly IReadOnlyCollection<ShardFailure> DefaultFailedShards =
	//	new ReadOnlyCollection<ShardFailure>(new ShardFailure[0]);

	/// <summary>
	/// Additional properties related to the error cause. Contains properties that
	/// are not explicitly mapped on <see cref="ErrorCause" />
	/// </summary>
	public IReadOnlyDictionary<string, object> AdditionalProperties { get; internal set; } = DefaultDictionary;

	/// <summary> The name of the Elasticsearch server exception that was thrown </summary>
	public string? Type { get; internal set; }

	/// <summary>
	/// If stacktrace was requested this holds the java stack trace as it occurred on the server
	/// </summary>
	[JsonPropertyName("stack_trace")]
	public string? StackTrace { get; internal set; }

	/// <summary>
	/// The exception message of the exception that was thrown on the server causing the request to fail
	/// </summary>
	public string? Reason { get; internal set; }

	// The following are all very specific to individual failures
	// Seeking to clean this up within Elasticsearch itself: https://github.com/elastic/elasticsearch/issues/27672
#pragma warning disable 1591
	//public long? BytesLimit { get; set; }

	//public long? BytesWanted { get; set; }

	public ErrorCause? CausedBy { get; internal set; }

	//public int? Column { get; set; }

	//public IReadOnlyCollection<ShardFailure> FailedShards { get; set; } = DefaultFailedShards;

	//public bool? Grouped { get; set; }

	public string? Index { get; internal set; }

	public string? IndexUUID { get; internal set; }

	//public string Language { get; set; }

	//public string LicensedExpiredFeature { get; set; }

	//public int? Line { get; set; }

	//public string Phase { get; set; }

	//public IReadOnlyCollection<string> ResourceId { get; set; } = DefaultCollection;

	//public string ResourceType { get; set; }

	//public string Script { get; set; }

	//public IReadOnlyCollection<string> ScriptStack { get; set; } = DefaultCollection;

	// TODO: This attribute is supported from 5.8 onward. At the moment Transport depends on that version but is that safe?
	//[JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
	//public int? Shard { get; set; }
#pragma warning restore 1591

	/// <summary> A human readable string representation of the exception returned by Elasticsearch </summary>
	public override string ToString() => CausedBy == null
		? $"Type: {Type} Reason: \"{Reason}\""
		: $"Type: {Type} Reason: \"{Reason}\" CausedBy: \"{CausedBy}\"";
}
