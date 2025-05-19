// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using Elastic.Transport.Products;

namespace Elastic.Transport;

/// <summary>
/// A failure in <see cref="RequestPipeline"/>'s workflow that caused it to end prematurely.
/// </summary>
public enum PipelineFailure
{
	/// <summary>
	/// The provided credentials were insufficient.
	/// <para>If this is thrown during an initial sniff or ping it short circuits and returns immediately</para>
	/// </summary>
	BadAuthentication,

	/// <summary>
	/// A bad response as determined by <see cref="ProductRegistration.HttpStatusCodeClassifier"/>
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
	/// The overall timeout specified by <see cref="IRequestConfiguration.MaxRetryTimeout"/> was reached
	/// </summary>
	MaxTimeoutReached,

	/// <summary>
	/// The overall max retries as specified by <see cref="IRequestConfiguration.MaxRetries"/> was reached
	/// </summary>
	MaxRetriesReached,

	/// <summary>
	/// An exception occurred during <see cref="RequestPipeline"/> that could not be handled
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

internal static class PipelineFailureExtensions
{
	public static string ToStringFast(this PipelineFailure failure) => failure switch
	{
		PipelineFailure.BadAuthentication => nameof(PipelineFailure.BadAuthentication),
		PipelineFailure.BadResponse => nameof(PipelineFailure.BadResponse),
		PipelineFailure.PingFailure => nameof(PipelineFailure.PingFailure),
		PipelineFailure.SniffFailure => nameof(PipelineFailure.SniffFailure),
		PipelineFailure.CouldNotStartSniffOnStartup => nameof(PipelineFailure.CouldNotStartSniffOnStartup),
		PipelineFailure.MaxTimeoutReached => nameof(PipelineFailure.MaxTimeoutReached),
		PipelineFailure.MaxRetriesReached => nameof(PipelineFailure.MaxRetriesReached),
		PipelineFailure.Unexpected => nameof(PipelineFailure.Unexpected),
		PipelineFailure.BadRequest => nameof(PipelineFailure.BadRequest),
		PipelineFailure.NoNodesAttempted => nameof(PipelineFailure.NoNodesAttempted),
		_ => throw new ArgumentOutOfRangeException(nameof(failure), failure, null)
	};
}
