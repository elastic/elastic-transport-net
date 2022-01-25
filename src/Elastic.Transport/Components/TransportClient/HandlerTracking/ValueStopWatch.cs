// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if DOTNETCORE
using System;
using System.Threading;

namespace Elastic.Transport
{
	/// <summary>
	/// A convenience API for interacting with System.Threading.Timer in a way
	/// that doesn't capture the ExecutionContext. We should be using this (or equivalent)
	/// everywhere we use timers to avoid rooting any values stored in asynclocals.
	/// <para> https://github.com/dotnet/runtime/blob/master/src/libraries/Common/src/Extensions/ValueStopwatch/ValueStopwatch.cs </para>
	/// </summary>
	internal static class NonCapturingTimer
	{
		public static Timer Create(TimerCallback callback, object state, TimeSpan dueTime, TimeSpan period)
		{
			if (callback == null) throw new ArgumentNullException(nameof(callback));

			// Don't capture the current ExecutionContext and its AsyncLocals onto the timer
			var restoreFlow = false;
			try
			{
				if (ExecutionContext.IsFlowSuppressed()) return new Timer(callback, state, dueTime, period);

				ExecutionContext.SuppressFlow();
				restoreFlow = true;

				return new Timer(callback, state, dueTime, period);
			}
			finally
			{
				// Restore the current ExecutionContext
				if (restoreFlow) ExecutionContext.RestoreFlow();
			}
		}
	}
}
#endif
