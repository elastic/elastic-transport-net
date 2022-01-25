// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;

namespace Elastic.Transport
{
	/// <summary> Provides some information to the transport auditing and diagnostics infrastructure about the serializer in use and its <see cref="Purpose"/> </summary>
	public class SerializerRegistrationInformation
	{
		private readonly string _stringRepresentation;

		/// <inheritdoc cref="SerializerRegistrationInformation"/>
		public SerializerRegistrationInformation(Type type, string purpose)
		{
			TypeInformation = type;
			Purpose = purpose;
			_stringRepresentation = $"{Purpose}: {TypeInformation.FullName}";
		}

		/// <summary> The type of <see cref="Serializer"/> in use currently </summary>
		// ReSharper disable once MemberCanBePrivate.Global
		public Type TypeInformation { get; }

		/// <summary>
		/// A string describing the purpose of the serializer emitting this events.
		/// <para>In `Elastisearch.Net` this will always be "request/response"</para>
		/// <para>Using `Nest` this could also be `source` allowing you to differentiate between the internal and configured source serializer</para>
		/// </summary>
		// ReSharper disable once MemberCanBePrivate.Global
		public string Purpose { get; }

		/// <summary> A precalculated string representation of the serializer in use </summary>
		public override string ToString() => _stringRepresentation;
	}
}
