// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Diagnostics;
using Elastic.Transport.Diagnostics.Auditing;

namespace Elastic.Transport.Diagnostics;

/// <summary>
/// Provides public access to the strings used while emitting diagnostics.
/// This makes wiring up <see cref="DiagnosticListener"/>'s less error prone and eliminates magic strings
/// </summary>
internal static class DiagnosticSources
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
	/// <see cref="IRequestInvoker"/> implementation.
	/// </summary>
	public class HttpConnectionDiagnosticKeys : IDiagnosticsKeys
	{
		/// TODO investigate if we can update our source name
		/// <inheritdoc cref="IDiagnosticsKeys.SourceName"/>
		public string SourceName { get; } = "Elastic.Transport.HttpTransportClient";

		/// <summary> Start and stop event initiating the request and sending and receiving the headers</summary>
		public string SendAndReceiveHeaders { get; } = nameof(SendAndReceiveHeaders);

		/// <summary> Start and stop event that tracks receiving the body</summary>
		public string ReceiveBody { get; } = nameof(ReceiveBody);
	}

	/// <summary>
	/// Provides access to the string event names related to serialization.
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
	/// Provides access to the string event names that <see cref="DiagnosticSources.RequestPipeline"/> emits
	/// </summary>
	public class RequestPipelineDiagnosticKeys : IDiagnosticsKeys
	{
		/// <inheritdoc cref="IDiagnosticsKeys.SourceName"/>
		public string SourceName { get; } = "RequestPipeline";

		/// <summary>
		/// Start and stop event around <see cref="RequestPipeline.CallProductEndpoint{TResponse}"/> invocations
		/// </summary>
		public string CallProductEndpoint { get; } = nameof(CallProductEndpoint);

		/// <summary> Start and stop event around <see cref="RequestPipeline.Ping"/> invocations</summary>
		public string Ping { get; } = nameof(Ping);

		/// <summary> Start and stop event around <see cref="RequestPipeline.Sniff"/> invocations</summary>
		public string Sniff { get; } = nameof(Sniff);
	}

	/// <summary>
	/// Reference to the diagnostic source name that allows you to listen to all decisions that
	/// <see cref="DiagnosticSources.RequestPipeline"/> makes. Events it emits are the names on <see cref="AuditEvent"/>
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
}
