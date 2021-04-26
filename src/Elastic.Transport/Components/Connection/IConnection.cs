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

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Elastic.Transport
{
	/// <summary>
	/// This interface abstracts the actual IO <see cref="ITransport{TConnectionSettings}"/> performs.
	/// <para><see cref="ITransport{TConnectionSettings}"/> holds a single instance of this class</para>
	/// <para>The instance to be used is provided to the constructor of <see cref="ITransportConfiguration"/> implementations</para>
	/// <para>Where its exposed under <see cref="ITransportConfiguration.Connection"/></para>
	/// </summary>
	public interface IConnection : IDisposable
	{
		/// <summary>
		/// Perform a request to the endpoint described by <paramref name="requestData"/> using its associated configuration.
		/// </summary>
		/// <param name="requestData">An object describing where and how to perform the IO call</param>
		/// <param name="cancellationToken"></param>
		/// <typeparam name="TResponse">
		/// An implementation of <see cref="ITransportResponse"/> ensuring enough information is available
		/// for <see cref="IRequestPipeline"/> and <see cref="ITransport{TConnectionSettings}"/> to determine what to
		/// do with the response
		/// </typeparam>
		/// <returns>
		/// An implementation of <see cref="ITransportResponse"/> ensuring enough information is available
		/// for <see cref="IRequestPipeline"/> and <see cref="ITransport{TConnectionSettings}"/> to determine what to
		/// do with the response
		/// </returns>
		Task<TResponse> RequestAsync<TResponse>(RequestData requestData, CancellationToken cancellationToken)
			where TResponse : class, ITransportResponse, new();

		/// <summary>
		/// Perform a request to the endpoint described by <paramref name="requestData"/> using its associated configuration.
		/// </summary>
		/// <param name="requestData">An object describing where and how to perform the IO call</param>
		/// <typeparam name="TResponse">
		/// An implementation of <see cref="ITransportResponse"/> ensuring enough information is available
		/// for <see cref="IRequestPipeline"/> and <see cref="ITransport{TConnectionSettings}"/> to determine what to
		/// do with the response
		/// </typeparam>
		/// <returns>
		/// An implementation of <see cref="ITransportResponse"/> ensuring enough information is available
		/// for <see cref="IRequestPipeline"/> and <see cref="ITransport{TConnectionSettings}"/> to determine what to
		/// do with the response
		/// </returns>
		TResponse Request<TResponse>(RequestData requestData)
			where TResponse : class, ITransportResponse, new();
	}
}
