// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.IO;

namespace Elastic.Transport.Tests.Components.Serialization;

public abstract class SerializerTestBase
{
	protected static Stream SerializeToStream<T>(T data)
	{
		var stream = new MemoryStream();
		LowLevelRequestResponseSerializer.Instance.Serialize(data, stream);
		stream.Position = 0;
		return stream;
	}
}
