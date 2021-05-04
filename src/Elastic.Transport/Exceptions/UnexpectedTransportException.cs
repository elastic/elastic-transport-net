// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using Elastic.Transport.Extensions;

namespace Elastic.Transport
{

	/// <summary>
	/// An exception occured that was not the result of one the well defined exit points as modelled by
	/// <see cref="PipelineFailure"/>. This exception will always bubble out.
	/// </summary>
	public class UnexpectedTransportException : TransportException
	{
		/// <inheritdoc cref="UnexpectedTransportException"/>
		public UnexpectedTransportException(Exception killerException, IReadOnlyCollection<PipelineException> seenExceptions)
			: base(PipelineFailure.Unexpected, killerException?.Message ?? "An unexpected exception occurred.", killerException) =>
			SeenExceptions = seenExceptions ?? EmptyReadOnly<PipelineException>.Collection;

		/// <summary>
		/// Seen Exceptions that we try to failover on before this <see cref="UnexpectedTransportException"/> was thrown.
		/// </summary>
		// ReSharper disable once MemberCanBePrivate.Global
		public IReadOnlyCollection<PipelineException> SeenExceptions { get; }
	}


}
