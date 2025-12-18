// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Collections;
using System.Collections.Generic;

namespace Elastic.Transport.Extensions;

internal struct EmptyEnumerator<T> : IEnumerator<T>
{
	public T Current => default!;
	object IEnumerator.Current => Current!;
	public bool MoveNext() => false;

	public void Reset()
	{
	}

	public void Dispose()
	{
	}
}
