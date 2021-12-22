// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Elastic.Transport.Diagnostics;

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

		/// <summary> The type of <see cref="SerializerBase"/> in use currently </summary>
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

	/// <summary>
	/// Wraps configured serializer so that we can emit diagnostics per configured serializer.
	/// </summary>
	public class DiagnosticsSerializerProxy : SerializerBase
	{
		private readonly SerializerBase _serializer;
		private readonly SerializerRegistrationInformation _state;
		private static DiagnosticSource DiagnosticSource { get; } = new DiagnosticListener(DiagnosticSources.Serializer.SourceName);

		/// <summary>
		/// <inheritdoc cref="DiagnosticsSerializerProxy" path="/summary"/>
		/// </summary>
		/// <param name="serializer">The serializer we are proxying</param>
		/// <param name="purpose"><inheritdoc cref="SerializerRegistrationInformation.Purpose" path="/summary"/></param>
		public DiagnosticsSerializerProxy(SerializerBase serializer, string purpose = "request/response")
		{
			_serializer = serializer;
			_state = new SerializerRegistrationInformation(serializer.GetType(), purpose);
		}

		/// <inheritdoc cref="SerializerBase.Deserialize"/>>
		public override object Deserialize(Type type, Stream stream)
		{
			using (DiagnosticSource.Diagnose(DiagnosticSources.Serializer.Deserialize, _state))
				return _serializer.Deserialize(type, stream);
		}

		/// <inheritdoc cref="SerializerBase.Deserialize{T}"/>>
		public override T Deserialize<T>(Stream stream)
		{
			using (DiagnosticSource.Diagnose(DiagnosticSources.Serializer.Deserialize, _state))
				return _serializer.Deserialize<T>(stream);
		}

		/// <inheritdoc cref="SerializerBase.DeserializeAsync"/>>
		public override Task<object> DeserializeAsync(Type type, Stream stream, CancellationToken cancellationToken = default)
		{
			using (DiagnosticSource.Diagnose(DiagnosticSources.Serializer.Deserialize, _state))
				return _serializer.DeserializeAsync(type, stream, cancellationToken);
		}

		/// <inheritdoc cref="SerializerBase.DeserializeAsync{T}"/>>
		public override Task<T> DeserializeAsync<T>(Stream stream, CancellationToken cancellationToken = default)
		{
			using (DiagnosticSource.Diagnose(DiagnosticSources.Serializer.Deserialize, _state))
				return _serializer.DeserializeAsync<T>(stream, cancellationToken);
		}

		/// <inheritdoc cref="SerializerBase.Serialize{T}"/>>
		public override void Serialize<T>(T data, Stream stream, SerializationFormatting formatting = SerializationFormatting.None)
		{
			using (DiagnosticSource.Diagnose(DiagnosticSources.Serializer.Serialize, _state))
				_serializer.Serialize(data, stream, formatting);
		}

		/// <inheritdoc cref="SerializerBase.SerializeAsync{T}"/>>
		public override Task SerializeAsync<T>(T data, Stream stream, SerializationFormatting formatting = SerializationFormatting.None,
			CancellationToken cancellationToken = default
		)
		{
			using (DiagnosticSource.Diagnose(DiagnosticSources.Serializer.Serialize, _state))
				return _serializer.SerializeAsync(data, stream, formatting, cancellationToken);
		}
	}
}
