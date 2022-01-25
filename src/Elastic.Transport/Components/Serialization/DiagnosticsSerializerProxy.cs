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
	/// <summary>
	/// Wraps configured serializer so that we can emit diagnostics per configured serializer.
	/// </summary>
	internal class DiagnosticsSerializerProxy : Serializer
	{
		private readonly Serializer _serializer;
		private readonly SerializerRegistrationInformation _state;
		private static DiagnosticSource DiagnosticSource { get; } = new DiagnosticListener(DiagnosticSources.Serializer.SourceName);

		/// <summary>
		/// <inheritdoc cref="DiagnosticsSerializerProxy" path="/summary"/>
		/// </summary>
		/// <param name="serializer">The serializer we are proxying</param>
		/// <param name="purpose"><inheritdoc cref="SerializerRegistrationInformation.Purpose" path="/summary"/></param>
		public DiagnosticsSerializerProxy(Serializer serializer, string purpose = "request/response")
		{
			_serializer = serializer;
			_state = new SerializerRegistrationInformation(serializer.GetType(), purpose);
		}

		/// <summary>
		/// Access to the inner <see cref="Serializer"/> wrapped by this proxy.
		/// </summary>
		public Serializer InnerSerializer => _serializer;

		/// <inheritdoc cref="Serializer.Deserialize"/>>
		public override object Deserialize(Type type, Stream stream)
		{
			using (DiagnosticSource.Diagnose(DiagnosticSources.Serializer.Deserialize, _state))
				return _serializer.Deserialize(type, stream);
		}

		/// <inheritdoc cref="Serializer.Deserialize{T}"/>>
		public override T Deserialize<T>(Stream stream)
		{
			using (DiagnosticSource.Diagnose(DiagnosticSources.Serializer.Deserialize, _state))
				return _serializer.Deserialize<T>(stream);
		}

		/// <inheritdoc cref="Serializer.DeserializeAsync"/>>
		public override Task<object> DeserializeAsync(Type type, Stream stream, CancellationToken cancellationToken = default)
		{
			using (DiagnosticSource.Diagnose(DiagnosticSources.Serializer.Deserialize, _state))
				return _serializer.DeserializeAsync(type, stream, cancellationToken);
		}

		/// <inheritdoc cref="Serializer.DeserializeAsync{T}"/>>
		public override Task<T> DeserializeAsync<T>(Stream stream, CancellationToken cancellationToken = default)
		{
			using (DiagnosticSource.Diagnose(DiagnosticSources.Serializer.Deserialize, _state))
				return _serializer.DeserializeAsync<T>(stream, cancellationToken);
		}

		/// <inheritdoc cref="Serializer.Serialize{T}"/>>
		public override void Serialize<T>(T data, Stream stream, SerializationFormatting formatting = SerializationFormatting.None)
		{
			using (DiagnosticSource.Diagnose(DiagnosticSources.Serializer.Serialize, _state))
				_serializer.Serialize(data, stream, formatting);
		}

		/// <inheritdoc cref="Serializer.SerializeAsync{T}"/>>
		public override Task SerializeAsync<T>(T data, Stream stream, SerializationFormatting formatting = SerializationFormatting.None,
			CancellationToken cancellationToken = default
		)
		{
			using (DiagnosticSource.Diagnose(DiagnosticSources.Serializer.Serialize, _state))
				return _serializer.SerializeAsync(data, stream, formatting, cancellationToken);
		}
	}
}
