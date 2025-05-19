// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Elastic.Transport.Diagnostics;

/// <summary>
/// Internal subclass of <see cref="Activity"/> that implements <see cref="IDisposable"/> to
/// make it easier to use.
/// </summary>
internal class Diagnostic<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TState> : Diagnostic<TState, TState>
{
	[RequiresUnreferencedCode(WriteOfTRequiresUnreferencedCode)]
	public Diagnostic(string operationName, DiagnosticSource source, TState state)
		: base(operationName, source, state) =>
		EndState = state;
}

internal class Diagnostic<
	[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TState,
	[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]TStateEnd> : Activity, IDisposable
{
	internal const string WriteOfTRequiresUnreferencedCode = "Only the properties of the T type will be preserved. Properties of referenced types and properties of derived types may be trimmed.";

	public static Diagnostic<TState, TStateEnd> Default { get; } = new Diagnostic<TState, TStateEnd>();

	private readonly DiagnosticSource _source;
	private TStateEnd _endState;
	private readonly bool _default;
	private bool _disposed;

	private Diagnostic() : base("__NOOP__") => _default = true;

	[RequiresUnreferencedCode(WriteOfTRequiresUnreferencedCode)]
	public Diagnostic(string operationName, DiagnosticSource source, TState state) : base(operationName)
	{
		_source = source;
		_source.StartActivity(SetStartTime(DateTime.UtcNow), state);
	}

	public TStateEnd EndState
	{
		get => _endState;
		internal set
		{
			//do not store state on default instance
			if (_default) return;
			_endState =  value;
		}
	}

	protected override void Dispose(bool disposing)
	{
		if (_disposed) return;

		if (disposing)
		{
#pragma warning disable IL2026
			//_source can be null if Default instance
			_source?.StopActivity(SetEndTime(DateTime.UtcNow), EndState);
#pragma warning restore IL2026
		}

		_disposed = true;

		base.Dispose(disposing);
	}
}
