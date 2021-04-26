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

#if !NETSTANDARD2_0 && !NETFRAMEWORK
using System;
#endif

namespace Elastic.Transport
{
	/// <summary>
	/// Describes the type of data the user wants transmit over the transport
	/// </summary>
	public enum PostType
	{
		/// <summary>
		/// A raw array of <see cref="byte"/>'s to be send over the wire
		/// <para>Instantiate using <see cref="PostData.Bytes"/></para>
		/// </summary>
		ByteArray,
#if !NETSTANDARD2_0 && !NETFRAMEWORK
		/// <summary>
		/// An instance of <see cref="ReadOnlyMemory{T}"/> where T is byte
		/// <para>Instantiate using <see cref="PostData.ReadOnlyMemory"/></para>
		/// </summary>
		ReadOnlyMemory,
#endif
		/// <summary>
		/// An instance of <see cref="string"/>
		/// <para>Instantiate using <see cref="PostData.String"/></para>
		/// </summary>
		LiteralString,

		/// <summary>
		/// An instance of a user provided value to be serialized by <see cref="ITransportSerializer"/>
		/// <para>Instantiate using <see cref="PostData.Serializable{T}"/></para>
		/// </summary>
		Serializable,

		/// <summary>
		/// An enumerable of <see cref="string"/> this will be serialized using ndjson multiline syntax
		/// <para>Instantiate using <see cref="PostData.MultiJson(System.Collections.Generic.IEnumerable{string})"/></para>
		/// </summary>
		EnumerableOfString,

		/// <summary>
		/// An enumerable of <see cref="object"/> this will be serialized using ndjson multiline syntax
		/// <para>Instantiate using <see cref="PostData.MultiJson{T}(System.Collections.Generic.IEnumerable{T})"/></para>
		/// </summary>
		EnumerableOfObject,

		/// <summary>
		/// The user provided a delegate to write the instance on <see cref="PostData"/> manually and directly
		/// <para>Instantiate using <see cref="PostData.StreamHandler{T}"/></para>
		/// </summary>
		StreamHandler,

	}
}
