// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.IO;
using System.Runtime.Serialization;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Elastic.Transport.Products.Elasticsearch
{
	/// <summary> Represents the error response as returned by Elasticsearch. </summary>
	[DataContract]
	public sealed class ElasticsearchServerError : ErrorResponse
	{
		/// <inheritdoc cref="ElasticsearchServerError"/>
		public ElasticsearchServerError() { }

		/// <inheritdoc cref="ElasticsearchServerError"/>
		public ElasticsearchServerError(Error error, int? statusCode)
		{
			Error = error;
			Status = statusCode.GetValueOrDefault(-1);
		}

		/// <summary> an <see cref="Error"/> object that represents the server exception that occurred</summary>
		[DataMember(Name = "error")]
		[JsonPropertyName("error")]
		public Error Error { get; init; }

		/// <summary> The HTTP status code returned from the server </summary>
		[DataMember(Name = "status")]
		[JsonPropertyName("status")]
		public int Status { get; init; } = -1;

		/// <summary>
		/// Try and create an instance of <see cref="ElasticsearchServerError"/> from <paramref name="stream"/>
		/// </summary>
		/// <returns>Whether a an instance of <see cref="ElasticsearchServerError"/> was created successfully</returns>
		public static bool TryCreate(Stream stream, out ElasticsearchServerError serverError)
		{
			try
			{
				serverError = Create(stream);
				return serverError != null;
			}
			catch
			{
				serverError = null;
				return false;
			}
		}

		/// <summary>
		/// Use the clients default <see cref="LowLevelRequestResponseSerializer"/> to create an instance
		/// of <see cref="ElasticsearchServerError"/> from <paramref name="stream"/>
		/// </summary>
		public static ElasticsearchServerError Create(Stream stream) =>
			LowLevelRequestResponseSerializer.Instance.Deserialize<ElasticsearchServerError>(stream);

		// ReSharper disable once UnusedMember.Global
		/// <inheritdoc cref="Create"/>
		public static ValueTask<ElasticsearchServerError> CreateAsync(Stream stream, CancellationToken token = default) =>
			LowLevelRequestResponseSerializer.Instance.DeserializeAsync<ElasticsearchServerError>(stream, token);

		/// <summary> A human readable string representation of the server error returned by Elasticsearch </summary>
		public override string ToString()
		{
			var sb = new StringBuilder();
			sb.Append($"ServerError: {Status}");
			if (Error != null)
				sb.Append(Error);
			return sb.ToString();
		}

		/// <summary>
		/// 
		/// </summary>
		/// <returns></returns>
		public override bool HasError() => Status > 0 && Error is not null;
	}
}
