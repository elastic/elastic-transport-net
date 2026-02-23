// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;

namespace Elastic.Transport;

/// <summary>
/// A pipeline exception is throw when ever a known failing exit point is reached in <see cref="RequestPipeline"/>
/// <para>See <see cref="PipelineFailure"/> for known exits points</para>
/// </summary>
public class PipelineException : Exception
{
	/// <inheritdoc cref="PipelineException"/>
	public PipelineException(PipelineFailure failure)
		: base(GetMessage(failure)) => FailureReason = failure;

	/// <inheritdoc cref="PipelineException"/>
	public PipelineException(PipelineFailure failure, Exception? innerException)
		: base(GetMessage(failure), innerException) => FailureReason = failure;

	/// <inheritdoc cref="PipelineFailure"/>
	public PipelineFailure FailureReason { get; }

	/// <summary>
	/// This exception is one the <see cref="ITransport{TConfiguration}"/> can handle
	/// <para><see cref="PipelineFailure.BadRequest"/></para>
	/// <para><see cref="PipelineFailure.BadResponse"/></para>
	/// <para><see cref="PipelineFailure.PingFailure"/></para>
	/// </summary>
	public bool Recoverable =>
		FailureReason is PipelineFailure.BadRequest
		or PipelineFailure.BadResponse
		or PipelineFailure.PingFailure;

	/// <summary> The response that triggered this exception </summary>
	public TransportResponse? Response { get; internal set; }

	private static string GetMessage(PipelineFailure failure) =>
		failure switch
		{
			PipelineFailure.BadRequest => "An error occurred trying to write the request data to the specified node.",
			PipelineFailure.BadResponse => "An error occurred trying to read the response from the specified node.",
			PipelineFailure.BadAuthentication => "Could not authenticate with the specified node. Try verifying your credentials or check your Shield configuration.",
			PipelineFailure.PingFailure => "Failed to ping the specified node.",
			PipelineFailure.SniffFailure => "Failed sniffing cluster state.",
			PipelineFailure.CouldNotStartSniffOnStartup => "Failed sniffing cluster state upon client startup.",
			PipelineFailure.MaxTimeoutReached => "Maximum timeout was reached.",
			PipelineFailure.MaxRetriesReached => "The call was retried the configured maximum amount of times",
			PipelineFailure.NoNodesAttempted => "No nodes were attempted, this can happen when a node predicate does not match any nodes",
			_ => "An unexpected error occurred. Try checking the original exception for more information.",
		};
}
