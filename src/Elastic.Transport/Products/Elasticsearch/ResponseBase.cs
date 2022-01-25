// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;
using Elastic.Transport.Products.Elasticsearch.Failures;

namespace Elastic.Transport.Products.Elasticsearch
{
	/// <summary>
	/// 
	/// </summary>
	public abstract class ResponseBase : IResponse
	{
		private IApiCallDetails? _originalApiCall;
		
		/// <summary> Returns useful information about the request(s) that were part of this API call. </summary>
		[JsonIgnore]
		public virtual IApiCallDetails? ApiCall => _originalApiCall;

		/// <summary>
		/// A collection of warnings returned from Elasticsearch.
		/// <para>Used to provide server warnings, for example, when the request uses an API feature that is marked as deprecated.</para>
		/// </summary>
		[JsonIgnore]
		public IEnumerable<string> Warnings
		{
			get
			{
				if (ApiCall.ParsedHeaders is not null && ApiCall.ParsedHeaders.TryGetValue("warning", out var warnings))
				{
					foreach (var warning in warnings)
						yield return warning;
				}
			}
		}

		/// <inheritdoc />
		[JsonIgnore]
		public string DebugInformation
		{
			get
			{
				var sb = new StringBuilder();
				sb.Append($"{(!IsValid ? "Inv" : "V")}alid Elastic.Clients.Elasticsearch response built from a ");
				sb.AppendLine(ApiCall?.ToString().ToCamelCase() ??
							"null ApiCall which is highly exceptional, please open a bug if you see this");
				if (!IsValid)
					DebugIsValid(sb);

				if (ApiCall.ParsedHeaders is not null && ApiCall.ParsedHeaders.TryGetValue("warning", out var warnings))
				{
					sb.AppendLine($"# Server indicated warnings:");

					foreach (var warning in warnings)
						sb.AppendLine($"- {warning}");
				}

				if (ApiCall != null)
					ResponseStatics.DebugInformationBuilder(ApiCall, sb);
				return sb.ToString();
			}
		}

		/// <inheritdoc />
		[JsonIgnore]
		public virtual bool IsValid
		{
			get
			{
				var statusCode = ApiCall?.HttpStatusCode;

				// TODO - Review this on a request by reqeust basis
				if (statusCode == 404)
					return false;

				return (ApiCall?.Success ?? false) && (!ServerError?.HasError() ?? true);
			}
		}

		/// <inheritdoc />
		[JsonIgnore]
		public Exception? OriginalException => ApiCall?.OriginalException;

		IApiCallDetails? ITransportResponse.ApiCall
		{
			get => _originalApiCall;
			set => _originalApiCall = value;
		}

		/// <summary>
		/// 
		/// </summary>
		[JsonIgnore]
		public ServerError ServerError { get; internal set; }

		/// <summary>
		/// 
		/// </summary>
		/// <param name="exception"></param>
		/// <returns></returns>
		// TODO: We need nullable annotations here ideally as exception is not null when the return value is true.
		public bool TryGetOriginalException(out Exception? exception)
		{
			if (OriginalException is not null)
			{
				exception = OriginalException;
				return true;
			}

			exception = null;
			return false;
		}

		/// <summary>Subclasses can override this to provide more information on why a call is not valid.</summary>
		protected virtual void DebugIsValid(StringBuilder sb) { }

		/// <inheritdoc />
		public override string ToString() =>
			$"{(!IsValid ? "Inv" : "V")}alid Elastic.Clients.Elasticsearch response built from a {ApiCall?.ToString().ToCamelCase()}";
	}
}
