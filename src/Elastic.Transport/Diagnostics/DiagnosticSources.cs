// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Diagnostics;
using Elastic.Transport.Diagnostics.Auditing;

namespace Elastic.Transport.Diagnostics
{
	/// <summary>
	/// Provides public access to the strings used while emitting diagnostics.
	/// This makes wiring up <see cref="DiagnosticListener"/>'s less error prone and eliminates magic strings
	/// </summary>
	public static class DiagnosticSources
	{
		/// <summary>
		/// When subscribing to <see cref="AuditDiagnosticKeys.SourceName"/> you will be notified of all decisions in the request pipeline
		/// </summary>
		public static AuditDiagnosticKeys AuditTrailEvents { get; } = new AuditDiagnosticKeys();

		/// <summary>
		/// When subscribing to <see cref="RequestPipelineDiagnosticKeys.SourceName"/> you will be notified every time a sniff/ping or an API call to Elasticsearch happens
		/// </summary>
		public static RequestPipelineDiagnosticKeys RequestPipeline { get; } = new RequestPipelineDiagnosticKeys();

		/// <summary>
		/// When subscribing to <see cref="HttpConnectionDiagnosticKeys.SourceName"/> you will be notified every time a a connection starts and stops a request and starts and stops a a response
		/// </summary>
		public static HttpConnectionDiagnosticKeys HttpConnection { get; } = new HttpConnectionDiagnosticKeys();

		/// <summary>
		/// When subscribing to <see cref="SerializerDiagnosticKeys.SourceName"/> you will be notified every time a particular serializer writes or reads
		/// </summary>
		public static SerializerDiagnosticKeys Serializer { get; } = new SerializerDiagnosticKeys();

		private interface IDiagnosticsKeys
		{
			// ReSharper disable once UnusedMemberInSuper.Global
			/// <summary>
			/// The source name to enable to receive diagnostic data for this <see cref="DiagnosticSource"/>
			/// </summary>
			string SourceName { get; }
		}

		/// <summary>
		/// Provides access to the string event names related to <see cref="HttpConnection"/> the default
		/// <see cref="TransportClient"/> implementation.
		/// </summary>
		public class HttpConnectionDiagnosticKeys : IDiagnosticsKeys
		{
			/// <inheritdoc cref="IDiagnosticsKeys.SourceName"/>
			public string SourceName { get; } = typeof(HttpTransportClient).FullName;

			/// <summary> Start and stop event initiating the request and sending and receiving the headers</summary>
			public string SendAndReceiveHeaders { get; } = nameof(SendAndReceiveHeaders);

			/// <summary> Start and stop event that tracks receiving the body</summary>
			public string ReceiveBody { get; } = nameof(ReceiveBody);
		}

		/// <summary>
		/// Provides access to the string event names related to <see cref="DiagnosticsSerializerProxy"/> which
		/// internally wraps any configured <see cref="Elastic.Transport.Serializer"/>
		/// </summary>
		public class SerializerDiagnosticKeys : IDiagnosticsKeys
		{
			/// <inheritdoc cref="IDiagnosticsKeys.SourceName"/>
			public string SourceName { get; } = typeof(Serializer).FullName;

			/// <summary> Start and stop event around <see cref="Serializer.Serialize{T}"/> invocations</summary>
			public string Serialize { get; } = nameof(Serialize);

			/// <summary> Start and stop event around <see cref="Serializer.Deserialize{T}"/> invocations</summary>
			public string Deserialize { get; } = nameof(Deserialize);
		}

		/// <summary>
		/// Provides access to the string event names that <see cref="RequestPipeline"/> emits
		/// </summary>
		public class RequestPipelineDiagnosticKeys : IDiagnosticsKeys
		{
			/// <inheritdoc cref="IDiagnosticsKeys.SourceName"/>
			public string SourceName { get; } = "RequestPipeline";

			/// <summary>
			/// Start and stop event around <see cref="IRequestPipeline.CallProductEndpoint{TResponse}"/> invocations
			/// </summary>
			public string CallProductEndpoint { get; } = nameof(CallProductEndpoint);

			/// <summary> Start and stop event around <see cref="IRequestPipeline.Ping"/> invocations</summary>
			public string Ping { get; } = nameof(Ping);

			/// <summary> Start and stop event around <see cref="IRequestPipeline.Sniff"/> invocations</summary>
			public string Sniff { get; } = nameof(Sniff);
		}

		/// <summary>
		/// Reference to the diagnostic source name that allows you to listen to all decisions that
		/// <see cref="IRequestPipeline"/> makes. Events it emits are the names on <see cref="AuditEvent"/>
		/// </summary>
		public class AuditDiagnosticKeys : IDiagnosticsKeys
		{
			/// <inheritdoc cref="IDiagnosticsKeys.SourceName"/>
			public string SourceName { get; } = typeof(Audit).FullName;
		}

		internal class EmptyDisposable : IDisposable
		{
			public void Dispose() { }
		}

		internal static EmptyDisposable SingletonDisposable { get; } = new EmptyDisposable();

		internal static IDisposable Diagnose<TState>(this DiagnosticSource source, string operationName, TState state)
		{
			if (!source.IsEnabled(operationName)) return SingletonDisposable;

			return new Diagnostic<TState>(operationName, source, state);
		}

		internal static Diagnostic<TState, TStateStop> Diagnose<TState, TStateStop>(this DiagnosticSource source, string operationName, TState state)
		{
			if (!source.IsEnabled(operationName)) return Diagnostic<TState, TStateStop>.Default;

			return new Diagnostic<TState, TStateStop>(operationName, source, state);
		}

		internal static Diagnostic<TState, TEndState> Diagnose<TState, TEndState>(this DiagnosticSource source, string operationName, TState state, TEndState endState)
		{
			if (!source.IsEnabled(operationName)) return Diagnostic<TState, TEndState>.Default;

			return new Diagnostic<TState, TEndState>(operationName, source, state)
			{
				EndState = endState
			};

		}

	}
}
