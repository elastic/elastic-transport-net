// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;

namespace Elastic.Transport.VirtualizedCluster.Rules;

/// <summary>
/// One of two values, for rule arguments that may be given either way: a number of times or <see cref="TimesHelper.Always"/>,
/// an exception or an HTTP status code. Conversions from either case are implicit, so rules read
/// <c>Fails(Times(2), 503)</c> and <c>FailAlways(new Exception())</c>.
/// </summary>
public union RuleOption<TFirst, TSecond>(TFirst, TSecond)
{
	/// <summary>Runs <paramref name="first"/> or <paramref name="second"/> depending on which case this holds.</summary>
	public void Match(Action<TFirst> first, Action<TSecond> second)
	{
		switch (this)
		{
			case TFirst f:
				first(f);
				break;
			case TSecond s:
				second(s);
				break;
			default:
				throw new InvalidOperationException("A RuleOption holds neither of its cases.");
		}
	}

	/// <summary>Returns the result of <paramref name="first"/> or <paramref name="second"/> depending on which case this holds.</summary>
	public T Match<T>(Func<TFirst, T> first, Func<TSecond, T> second) => this switch
	{
		TFirst f => first(f),
		TSecond s => second(s),
		_ => throw new InvalidOperationException("A RuleOption holds neither of its cases."),
	};
}
