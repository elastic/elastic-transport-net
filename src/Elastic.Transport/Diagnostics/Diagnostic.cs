/*
 * Licensed to Elasticsearch B.V. under one or more contributor
 * license agreements. See the NOTICE file distributed with
 * this work for additional information regarding copyright
 * ownership. Elasticsearch B.V. licenses this file to you under
 * the Apache License, Version 2.0 (the "License"); you may
 * not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *    http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing,
 * software distributed under the License is distributed on an
 * "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
 * KIND, either express or implied.  See the License for the
 * specific language governing permissions and limitations
 * under the License.
 */

using System;
using System.Diagnostics;

namespace Elastic.Transport.Diagnostics
{
	/// <summary>
	/// Internal subclass of <see cref="Activity"/> that implements <see cref="IDisposable"/> to
	/// make it easier to use.
	/// </summary>
	internal class Diagnostic<TState> : Diagnostic<TState, TState>
	{
		public Diagnostic(string operationName, DiagnosticSource source, TState state)
			: base(operationName, source, state) =>
			EndState = state;
	}

	internal class Diagnostic<TState, TStateEnd> : Activity, IDisposable
	{
		public static Diagnostic<TState, TStateEnd> Default { get; } = new Diagnostic<TState, TStateEnd>();

		private readonly DiagnosticSource _source;
		private TStateEnd _endState;
		private readonly bool _default;
		private bool _disposed;

		private Diagnostic() : base("__NOOP__") => _default = true;

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
				//_source can be null if Default instance
				_source?.StopActivity(SetEndTime(DateTime.UtcNow), EndState);
			}

			_disposed = true;

			base.Dispose(disposing);
		}
	}
}
