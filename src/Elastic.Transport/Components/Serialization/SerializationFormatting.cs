// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

namespace Elastic.Transport
{
	/// <summary>
	/// A hint to <see cref="SerializerBase"/> how to format the json.
	/// Implementation of <see cref="SerializerBase"/> might choose to ignore this hint though.
	/// </summary>
	public enum SerializationFormatting
	{
		/// <summary>
		/// Serializer should not render the json with whitespace and line endings. <see cref="SerializerBase"/>
		/// implementation HAVE to be able to adhere this value as for instance nd-json relies on this
		/// </summary>
		None,

		/// <summary>
		/// A hint that the user prefers readable data being written. <see cref="SerializerBase"/> implementations
		/// should try to adhere to this but won't break anything if they don't.
		/// </summary>
		Indented
	}
}
