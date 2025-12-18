// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Elastic.Transport.Extensions;

namespace Elastic.Transport;

/// <summary>
/// Represents a unique, case-insensitive, immutable collection of header names.
/// </summary>
public readonly struct HeadersList : IEnumerable<string>
{
	private readonly List<string> _headers = [];

	/// <summary>
	/// Create a new <see cref="HeadersList"/> from an existing enumerable of header names.
	/// Duplicate names, including those which only differ by case, will be ignored.
	/// </summary>
	/// <param name="headers">The header names to initialise the <see cref="HeadersList"/> with.</param>
	public HeadersList(IEnumerable<string> headers)
	{
		foreach (var header in headers)
		{
			if (!_headers.Contains(header, StringComparer.OrdinalIgnoreCase))
				_headers.Add(header);
		}
	}

	/// <summary> Represents a unique, case-insensitive, immutable collection of header names.  </summary>
	public HeadersList(IEnumerable<string> headers, string additionalHeader)
	{
		foreach (var header in headers)
		{
			if (!_headers.Contains(header, StringComparer.OrdinalIgnoreCase))
				_headers.Add(header);
		}

		if (!_headers.Contains(additionalHeader, StringComparer.OrdinalIgnoreCase))
			_headers.Add(additionalHeader);
	}

	/// <summary> Represents a unique, case-insensitive, immutable collection of header names. </summary>
	public HeadersList(IEnumerable<string> headers, IEnumerable<string> otherHeaders)
		: this(new HeadersList(headers), new HeadersList(otherHeaders))
	{
	}

	/// <summary>
	/// Initializes a new instance of <see cref="HeadersList"/> by combining two existing <see cref="HeadersList"/> instances.
	/// Duplicate names, including those which only differ by case, will be ignored.
	/// </summary>
	/// <param name="headers">The first set of header names to initialize the <see cref="HeadersList"/> with.</param>
	/// <param name="otherHeaders">The second set of header names to initialize the <see cref="HeadersList"/> with.</param>
	public HeadersList(HeadersList? headers, HeadersList? otherHeaders)
	{
		AddToHeaders(headers);
		AddToHeaders(otherHeaders);
	}

	private void AddToHeaders(HeadersList? headers)
	{
		if (headers is null)
			return;

		foreach (var header in headers)
		{
			if (!_headers.Contains(header, StringComparer.OrdinalIgnoreCase))
				_headers.Add(header);
		}
	}

	/// <summary>
	/// Create a new <see cref="HeadersList"/> initialised with a single header name.
	/// </summary>
	/// <param name="header">The header name to initialise the <see cref="HeadersList"/> with.</param>
	public HeadersList(string header) => _headers = [header];

	/// <summary>
	/// Gets the number of elements contained in the <see cref="HeadersList"/>.
	/// </summary>
	public int Count => _headers.Count;

	// ReSharper disable once ConstantConditionalAccessQualifier
	// ReSharper disable once ConstantNullCoalescingCondition
	/// <inheritdoc />
	public IEnumerator<string> GetEnumerator() => _headers?.GetEnumerator() ?? (IEnumerator<string>)new EmptyEnumerator<string>();

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

}
