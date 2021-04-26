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

using System.Dynamic;

namespace Elastic.Transport
{
	/// <summary>
	/// A type of response that makes it easier to work with responses in an untyped fashion.
	///
	/// <para>It exposes the body as <see cref="DynamicDictionary"/> which is `dynamic` through <see cref="DynamicObject"/></para>
	/// <para></para>
	/// <para>Since `dynamic` can be scary in .NET this response also exposes a safe traversal mechanism under
	/// <see cref="Get{T}"/> which support an xpath'esque syntax to fish for values in the returned json.
	/// </para>
	/// </summary>
	public class DynamicResponse : TransportResponseBase<DynamicDictionary>
	{
		/// <inheritdoc cref="DynamicResponse"/>
		public DynamicResponse() { }

		/// <inheritdoc cref="DynamicResponse"/>
		public DynamicResponse(DynamicDictionary dictionary)
		{
			Body = dictionary;
			Dictionary = dictionary;
		}

		private DynamicDictionary Dictionary { get; }

		/// <summary>
		/// Traverses data using path notation.
		/// <para><c>e.g some.deep.nested.json.path</c></para>
		/// <para> A special lookup is available for ANY key using <c>_arbitrary_key_</c> <c>e.g some.deep._arbitrary_key_.json.path</c> which will traverse into the first key</para>
		/// </summary>
		/// <param name="path">path into the stored object, keys are separated with a dot and the last key is returned as T</param>
		/// <typeparam name="T"></typeparam>
		/// <returns>T or default</returns>
		public T Get<T>(string path) => Dictionary.Get<T>(path);
	}
}
