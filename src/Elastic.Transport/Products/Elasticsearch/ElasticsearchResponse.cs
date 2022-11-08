// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace Elastic.Transport.Products.Elasticsearch
{
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
		/// <returns></returns>
		[JsonIgnore]
		public string DebugInformation
		{
			get
			{
				var sb = new StringBuilder();
				sb.Append($"{(!IsValid ? "Inv" : "V")}alid Elastic.Clients.Elasticsearch response built from a ");
				sb.AppendLine(ApiCallDetails?.ToString().ToCamelCase() ??
							"null ApiCall which is highly exceptional, please open a bug if you see this");
				if (!IsValid)
					DebugIsValid(sb);

				if (ApiCallDetails.ParsedHeaders is not null && ApiCallDetails.ParsedHeaders.TryGetValue("warning", out var warnings))
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
		/// 
		/// </summary>
		/// <returns></returns>
		[JsonIgnore]
		public virtual bool IsValid
		{
			get
			{
				var statusCode = ApiCallDetails?.HttpStatusCode;

				// TODO - Review this on a request by reqeust basis
				if (statusCode == 404)
					return false;

				return (ApiCallDetails?.Success ?? false) && (!ServerError?.HasError() ?? true);
			}
		}

		/// <summary>
		/// 
		/// </summary>
		[JsonIgnore]
		public ElasticsearchServerError ServerError { get; internal set; }

		/// <summary>
		/// 
		/// </summary>
		/// <param name="exception"></param>
		/// <returns></returns>
		// TODO: We need nullable annotations here ideally as exception is not null when the return value is true.
		public bool TryGetOriginalException(out Exception? exception)
		{
			if (ApiCallDetails.OriginalException is not null)
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
			$"{(!IsValid ? "Inv" : "V")}alid Elastic.Clients.Elasticsearch response built from a {ApiCallDetails?.ToString().ToCamelCase()}";
	}
}
