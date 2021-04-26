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
