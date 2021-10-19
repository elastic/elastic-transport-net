// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Elastic.Transport
{
	/// <summary>
	/// Represents a unique, case-insensitive, immutable collection of header names.
	/// </summary>
	public struct HeadersList : IEnumerable<string>
	{
		private readonly List<string> _headers;

		/// <summary>
		/// Create a new <see cref="HeadersList"/> from an existing enumerable of header names.
		/// Duplicate names, including those which only differ by case, will be ignored.
		/// </summary>
		/// <param name="headers">The header names to initialise the <see cref="HeadersList"/> with.</param>
		public HeadersList(IEnumerable<string> headers)
		{
			_headers = new List<string>();

			foreach (var header in headers)
			{
				if (!_headers.Contains(header, StringComparer.OrdinalIgnoreCase))
				{
					_headers.Add(header);
				}
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="headers"></param>
		/// <param name="additionalHeader"></param>
		public HeadersList(IEnumerable<string> headers, string additionalHeader)
		{
			_headers = new List<string>();

			foreach (var header in headers)
			{
				if (!_headers.Contains(header, StringComparer.OrdinalIgnoreCase))
				{
					_headers.Add(header);
				}
			}

			if (!_headers.Contains(additionalHeader, StringComparer.OrdinalIgnoreCase))
			{
				_headers.Add(additionalHeader);
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="headers"></param>
		/// <param name="otherHeaders"></param>
		public HeadersList(IEnumerable<string> headers, IEnumerable<string> otherHeaders)
		{
			_headers = new List<string>();

			foreach (var header in headers)
			{
				if (!_headers.Contains(header, StringComparer.OrdinalIgnoreCase))
				{
					_headers.Add(header);
				}
			}

			foreach (var header in otherHeaders)
			{
				if (!_headers.Contains(header, StringComparer.OrdinalIgnoreCase))
				{
					_headers.Add(header);
				}
			}
		}

		/// <summary>
		/// Create a new <see cref="HeadersList"/> initialised with a single header name.
		/// </summary>
		/// <param name="header">The header name to initialise the <see cref="HeadersList"/> with.</param>
		public HeadersList(string header) => _headers = new List<string> { header };

		/// <summary>
		/// Gets the number of elements contained in the <see cref="HeadersList"/>.
		/// </summary>
		public int Count => _headers is null ? 0 : _headers.Count;

		/// <inheritdoc />
		public IEnumerator<string> GetEnumerator() => _headers?.GetEnumerator() ?? (IEnumerator<string>)new EmptyEnumerator<string>();

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		internal struct EmptyEnumerator<T> : IEnumerator<T>
		{
			public T Current => default;
			object IEnumerator.Current => Current;
			public bool MoveNext() => false;

			public void Reset()
			{
			}

			public void Dispose()
			{
			}
		}
	}
}
