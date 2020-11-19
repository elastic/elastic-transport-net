// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.IO;
using System.Runtime.Serialization;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Elastic.Transport.Products.Elasticsearch.Failures
{
	/// <summary> Represents the error response as returned by Elasticsearch. </summary>
	[DataContract]
	public class ServerError
	{
		/// <inheritdoc cref="ServerError"/>
		public ServerError() { }

		/// <inheritdoc cref="ServerError"/>
		public ServerError(Error error, int? statusCode)
		{
			Error = error;
			Status = statusCode.GetValueOrDefault(-1);
		}

		/// <summary> an <see cref="Error"/> object that represents the server exception that occurred</summary>
		[DataMember(Name = "error")]
		[JsonPropertyName("error")]
		public Error Error { get; set; }

		/// <summary> The HTTP status code returned from the server </summary>
		[DataMember(Name = "status")]
		[JsonPropertyName("status")]
		public int Status { get; set; } = -1;

		/// <summary>
		/// Try and create an instance of <see cref="ServerError"/> from <paramref name="stream"/>
		/// </summary>
		/// <returns>Whether a an instance of <see cref="ServerError"/> was created successfully</returns>
		public static bool TryCreate(Stream stream, out ServerError serverError)
		{
			try
			{
				serverError = Create(stream);
				return true;
			}
			catch
			{
				serverError = null;
				return false;
			}
		}

		/// <summary>
		/// Use the clients default <see cref="LowLevelRequestResponseSerializer"/> to create an instance
		/// of <see cref="ServerError"/> from <paramref name="stream"/>
		/// </summary>
		public static ServerError Create(Stream stream) =>
			LowLevelRequestResponseSerializer.Instance.Deserialize<ServerError>(stream);

		// ReSharper disable once UnusedMember.Global
		/// <inheritdoc cref="Create"/>
		public static Task<ServerError> CreateAsync(Stream stream, CancellationToken token = default) =>
			LowLevelRequestResponseSerializer.Instance.DeserializeAsync<ServerError>(stream, token);

		/// <summary> A human readable string representation of the server error returned by Elasticsearch </summary>
		public override string ToString()
		{
			var sb = new StringBuilder();
			sb.Append($"ServerError: {Status}");
			if (Error != null)
				sb.Append(Error);
			return sb.ToString();
		}
	}
}
