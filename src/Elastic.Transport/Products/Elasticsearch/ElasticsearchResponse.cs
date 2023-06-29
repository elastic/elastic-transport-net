// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace Elastic.Transport.Products.Elasticsearch;

/// <summary>
/// Base response for Elasticsearch responses.
/// </summary>
public abstract class ElasticsearchResponse : TransportResponse
{
	/// <summary>
	/// A collection of warnings returned from Elasticsearch.
	/// <para>Used to provide server warnings, for example, when the request uses an API feature that is marked as deprecated.</para>
	/// </summary>
	[JsonIgnore]
	public IEnumerable<string> ElasticsearchWarnings
	{
		get
		{
			if (ApiCallDetails.ParsedHeaders is not null && ApiCallDetails.ParsedHeaders.TryGetValue("warning", out var warnings))
			{
				foreach (var warning in warnings)
					yield return warning;
			}
		}
	}

	/// <summary>
	///
	/// </summary>
	[JsonIgnore]
	public string DebugInformation
	{
		get
		{
			var sb = new StringBuilder();
			sb.Append($"{(!IsValidResponse ? "Inv" : "V")}alid Elasticsearch response built from a ");
			sb.AppendLine(ApiCallDetails?.ToString().ToCamelCase() ??
						"null ApiCall which is highly exceptional, please open a bug if you see this");
			if (!IsValidResponse)
				DebugIsValid(sb);

			if (ApiCallDetails?.ParsedHeaders is not null && ApiCallDetails.ParsedHeaders.TryGetValue("warning", out var warnings))
			{
				sb.AppendLine($"# Server indicated warnings:");

				foreach (var warning in warnings)
					sb.AppendLine($"- {warning}");
			}

			if (ApiCallDetails != null)
				Diagnostics.ResponseStatics.DebugInformationBuilder(ApiCallDetails, sb);

			return sb.ToString();
		}
	}

	/// <summary>
	/// Shortcut to test if the response is considered successful.
	/// </summary>
	/// <returns>A <see cref="bool"/> indicating success or failure.</returns>
	[JsonIgnore]
	public virtual bool IsValidResponse
	{
		get
		{
			var statusCode = ApiCallDetails?.HttpStatusCode;

			if (statusCode == 404)
				return false;

			return (ApiCallDetails?.HasSuccessfulStatusCodeAndExpectedContentType ?? false) && (!ElasticsearchServerError?.HasError() ?? true);
		}
	}

	/// <summary>
	///
	/// </summary>
	[JsonIgnore]
	public ElasticsearchServerError ElasticsearchServerError { get; internal set; }

	/// <summary>
	///
	/// </summary>
	/// <param name="exception"></param>
	/// <returns></returns>
	// TODO: We need nullable annotations here ideally as exception is not null when the return value is true.
	public bool TryGetOriginalException(out Exception? exception)
	{
		if (ApiCallDetails?.OriginalException is not null)
		{
			exception = ApiCallDetails.OriginalException;
			return true;
		}

		exception = null;
		return false;
	}

	/// <summary>Subclasses can override this to provide more information on why a call is not valid.</summary>
	protected virtual void DebugIsValid(StringBuilder sb) { }

	/// <summary>
	///
	/// </summary>
	/// <returns></returns>
	public override string ToString() =>
		$"{(!IsValidResponse ? "Inv" : "V")}alid response built from a {ApiCallDetails?.ToString().ToCamelCase()}";
}
