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
	/// Represents a unique, case-insensitive collection of header names.
	/// </summary>
	public struct HeadersList : IEnumerable<string>
	{
		private List<string> _headers;

		/// <summary>
		/// Create a new <see cref="HeadersList"/> from an existing enumerable of header names.
		/// Duplicate names, including those which only differ by case, will be ignored.
		/// </summary>
		/// <param name="headers">The header names to initialise the <see cref="HeadersList"/> with.</param>
		public HeadersList(IEnumerable<string> headers)
		{
			_headers = new List<string>();

			foreach (var header in headers)
				TryAdd(header);
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

		/// <summary>
		/// Attempt to add a header to the <see cref="HeadersList"/>.
		/// Duplicate names, including those which only differ by case, will be ignored.
		/// </summary>
		/// <param name="header">The header name to add to the <see cref="HeadersList"/>.</param>
		/// <returns></returns>
		public bool TryAdd(string header)
		{
			if (_headers is null)
			{
				_headers = new List<string>()
				{
					header
				};
				return true;
			}

			if (!_headers.Contains(header, StringComparer.OrdinalIgnoreCase))
			{
				_headers.Add(header);
				return true;
			}

			return false;
		}

		/// <summary>
		/// Attempt to remove a header from the list if it is present.
		/// </summary>
		/// <param name="header">The header name to remove from the <see cref="HeadersList"/>.</param>
		public void Remove(string header)
		{
			if (_headers is null) return;
			_headers.RemoveAll(s => s.Equals(header, StringComparison.OrdinalIgnoreCase));
		}

		/// <summary>
		/// Modifies the current <see cref="HeadersList"/> to contain all elements that are present in itself, the specified collection, or both.
		/// </summary>
		/// <param name="other">The collection to compare to the current <see cref="HeadersList"/> object.</param>
		public void UnionWith(IEnumerable<string> other)
		{
			foreach (var header in other)
				TryAdd(header);
		}

		/// <inheritdoc />
		public IEnumerator<string> GetEnumerator() => _headers?.GetEnumerator() ?? (IEnumerator<string>)Array.Empty<string>().GetEnumerator();

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	}
}
