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

using Elastic.Transport.Products;

namespace Elastic.Transport
{
	/// <summary>
	/// A failure in <see cref="RequestPipeline{TConfiguration}"/>'s workflow that caused it to end prematurely.
	/// </summary>
	public enum PipelineFailure
	{
		/// <summary>
		/// The provided credentials were insufficient.
		/// <para>If this is thrown during an initial sniff or ping it short circuits and returns immediately</para>
		/// </summary>
		BadAuthentication,

		/// <summary>
		/// A bad response as determined by <see cref="IProductRegistration.HttpStatusCodeClassifier"/>
		/// </summary>
		BadResponse,

		/// <summary> A ping request was unsuccessful</summary>
		PingFailure,
		/// <summary> A sniff request was unsuccessful</summary>
		SniffFailure,

		/// <summary>
		/// See <see cref="ITransportConfiguration.SniffsOnStartup"/> was requested but the first API call failed to sniff
		/// </summary>
		CouldNotStartSniffOnStartup,

		/// <summary>
		/// The overall timeout specified by <see cref="ITransportConfiguration.MaxRetryTimeout"/> was reached
		/// </summary>
		MaxTimeoutReached,

		/// <summary>
		/// The overall max retries as specified by <see cref="ITransportConfiguration.MaxRetries"/> was reached
		/// </summary>
		MaxRetriesReached,

		/// <summary>
		/// An exception occurred during <see cref="RequestPipeline{TConfiguration}"/> that could not be handled
		/// </summary>
		Unexpected,

		/// <summary> An exception happened while sending the request and a response was never fetched </summary>
		BadRequest,

		/// <summary>
		/// Rare but if <see cref="ITransportConfiguration.NodePredicate"/> is too stringent it could mean no
		/// nodes were considered for the API call
		/// </summary>
		NoNodesAttempted
	}
}
