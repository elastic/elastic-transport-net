// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;

namespace Elastic.Transport
{
	/// <summary>
	/// A pipeline exception is throw when ever a known failing exit point is reached in <see cref="RequestPipeline{TConfiguration}"/>
	/// <para>See <see cref="PipelineFailure"/> for known exits points</para>
	/// </summary>
	public class PipelineException : Exception
	{
		/// <inheritdoc cref="PipelineException"/>
		public PipelineException(PipelineFailure failure)
			: base(GetMessage(failure)) => FailureReason = failure;

		/// <inheritdoc cref="PipelineException"/>
		public PipelineException(PipelineFailure failure, Exception innerException)
			: base(GetMessage(failure), innerException) => FailureReason = failure;

		/// <inheritdoc cref="PipelineFailure"/>
		public PipelineFailure FailureReason { get; }

		/// <summary>
		/// This exception is one the <see cref="ITransport{TConnectionSettings}"/> can handle
		/// <para><see cref="PipelineFailure.BadRequest"/></para>
		/// <para><see cref="PipelineFailure.BadResponse"/></para>
		/// <para><see cref="PipelineFailure.PingFailure"/></para>
		/// </summary>
		public bool Recoverable =>
			FailureReason == PipelineFailure.BadRequest
			|| FailureReason == PipelineFailure.BadResponse
			|| FailureReason == PipelineFailure.PingFailure;

		//TODO why do we have both Response and ApiCall?

		/// <summary> The response that triggered this exception </summary>
		public TransportResponse Response { get; internal set; }

		/// <summary> The response that triggered this exception </summary>
		public ApiCallDetails ApiCall { get; internal set; }

		private static string GetMessage(PipelineFailure failure)
		{
			switch (failure)
			{
				case PipelineFailure.BadRequest: return "An error occurred trying to write the request data to the specified node.";
				case PipelineFailure.BadResponse: return "An error occurred trying to read the response from the specified node.";
				case PipelineFailure.BadAuthentication:
					return "Could not authenticate with the specified node. Try verifying your credentials or check your Shield configuration.";
				case PipelineFailure.PingFailure: return "Failed to ping the specified node.";
				case PipelineFailure.SniffFailure: return "Failed sniffing cluster state.";
				case PipelineFailure.CouldNotStartSniffOnStartup: return "Failed sniffing cluster state upon client startup.";
				case PipelineFailure.MaxTimeoutReached: return "Maximum timeout was reached.";
				case PipelineFailure.MaxRetriesReached: return "The call was retried the configured maximum amount of times";
				case PipelineFailure.NoNodesAttempted:
					return "No nodes were attempted, this can happen when a node predicate does not match any nodes";
				case PipelineFailure.Unexpected:
				default:
					return "An unexpected error occurred. Try checking the original exception for more information.";
			}
		}
	}
}
