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

namespace Elastic.Transport
{
	/// <summary>
	/// A hint to <see cref="ITransportSerializer"/> how to format the json.
	/// Implementation of <see cref="ITransportSerializer"/> might choose to ignore this hint though.
	/// </summary>
	public enum SerializationFormatting
	{
		/// <summary>
		/// Serializer should not render the json with whitespace and line endings. <see cref="ITransportSerializer"/>
		/// implementation HAVE to be able to adhere this value as for instance nd-json relies on this
		/// </summary>
		None,

		/// <summary>
		/// A hint that the user prefers readable data being written. <see cref="ITransportSerializer"/> implementations
		/// should try to adhere to this but won't break anything if they don't.
		/// </summary>
		Indented
	}
}
