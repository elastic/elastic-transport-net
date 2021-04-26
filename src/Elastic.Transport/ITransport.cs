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

using System.Threading;
using System.Threading.Tasks;

namespace Elastic.Transport
{
	/// <summary>
	/// Represents a transport you can call requests, it is recommended to implement <see cref="ITransport{TSettings}"/>
	/// </summary>
	public interface ITransport
	{
		/// <summary>
		/// Perform a request into the products cluster using <see cref="IRequestPipeline"/>'s workflow.
		/// </summary>
		TResponse Request<TResponse>(
			HttpMethod method,
			string path,
			PostData data = null,
			IRequestParameters requestParameters = null
		)
			where TResponse : class, ITransportResponse, new();

		/// <inheritdoc cref="Request{TResponse}"/>
		Task<TResponse> RequestAsync<TResponse>(
			HttpMethod method,
			string path,
			CancellationToken ctx,
			PostData data = null,
			IRequestParameters requestParameters = null
		)	where TResponse : class, ITransportResponse, new();
	}

	/// <summary>
	/// Transport coordinates the client requests over the connection pool nodes and is in charge of falling over on different nodes
	/// </summary>
	public interface ITransport<out TConfiguration> : ITransport
		where TConfiguration : ITransportConfiguration
	{
		/// <summary>
		/// The <see cref="ITransportConfiguration"/> in use by this transport instance
		/// </summary>
		TConfiguration Settings { get; }
	}
}
