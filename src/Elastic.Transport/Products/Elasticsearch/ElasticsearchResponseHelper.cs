// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Elastic.Transport.Diagnostics;

namespace Elastic.Transport.Products.Elasticsearch;

/// <summary>
/// Shared implementation logic for <see cref="IElasticsearchResponse"/> properties.
/// Used by all concrete Elasticsearch response types to avoid duplication.
/// </summary>
internal static class ElasticsearchResponseHelper
{
	public static bool IsValidResponse(ApiCallDetails? apiCallDetails, ElasticsearchServerError? serverError)
	{
		var statusCode = apiCallDetails?.HttpStatusCode;

		if (statusCode == 404)
			return false;

		return (apiCallDetails?.HasSuccessfulStatusCodeAndExpectedContentType ?? false)
			&& (!serverError?.HasError() ?? true);
	}

	public static IEnumerable<string> GetElasticsearchWarnings(ApiCallDetails? apiCallDetails)
	{
		if (apiCallDetails?.ParsedHeaders is not null && apiCallDetails.ParsedHeaders.TryGetValue("warning", out var warnings))
		{
			foreach (var warning in warnings)
				yield return warning;
		}
	}

	public static string GetDebugInformation(bool isValidResponse, ApiCallDetails? apiCallDetails, ElasticsearchServerError? serverError)
	{
		var sb = new StringBuilder();
		_ = sb.Append($"{(!isValidResponse ? "Inv" : "V")}alid Elasticsearch response built from a ");
		_ = sb.AppendLine(apiCallDetails?.ToString().ToCamelCase() ??
					"null ApiCall which is highly exceptional, please open a bug if you see this");
		if (!isValidResponse && serverError?.HasError() == true)
			_ = sb.AppendLine($"# ServerError: {serverError}");

		if (apiCallDetails?.ParsedHeaders is not null && apiCallDetails.ParsedHeaders.TryGetValue("warning", out var warnings))
		{
			_ = sb.AppendLine($"# Server indicated warnings:");

			foreach (var warning in warnings)
				_ = sb.AppendLine($"- {warning}");
		}

		if (apiCallDetails != null)
			_ = ResponseStatics.DebugInformationBuilder(apiCallDetails, sb);

		return sb.ToString();
	}

	/// <summary>
	/// Sets <see cref="ElasticsearchServerError"/> on a response via pattern matching on the concrete type.
	/// Needed because <see cref="IElasticsearchResponse.ElasticsearchServerError"/> only exposes a getter;
	/// the internal setter is on each concrete type.
	/// </summary>
	public static void SetServerError(TransportResponse response, ElasticsearchServerError error)
	{
		switch (response)
		{
			case ElasticsearchResponse r: r.ElasticsearchServerError = error; break;
			case ElasticsearchStringResponse r: r.ElasticsearchServerError = error; break;
			case ElasticsearchBytesResponse r: r.ElasticsearchServerError = error; break;
			case ElasticsearchStreamResponse r: r.ElasticsearchServerError = error; break;
			case ElasticsearchVoidResponse r: r.ElasticsearchServerError = error; break;
			case ElasticsearchDynamicResponse r: r.ElasticsearchServerError = error; break;
			case ElasticsearchJsonResponse r: r.ElasticsearchServerError = error; break;
#if NET10_0_OR_GREATER
			case ElasticsearchPipeResponse r: r.ElasticsearchServerError = error; break;
#endif
		}
	}

	public static bool TryGetOriginalException(ApiCallDetails? apiCallDetails, out Exception? exception)
	{
		if (apiCallDetails?.OriginalException is not null)
		{
			exception = apiCallDetails.OriginalException;
			return true;
		}

		exception = null;
		return false;
	}
}
