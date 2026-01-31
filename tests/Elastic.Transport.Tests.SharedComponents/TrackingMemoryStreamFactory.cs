// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

namespace Elastic.Transport.Tests.SharedComponents;

public class TrackingMemoryStreamFactory : MemoryStreamFactory
{
	public IList<TrackDisposeStream> Created { get; private set; } = [];

	public override MemoryStream Create()
	{
		var stream = new TrackDisposeStream();
		Created.Add(stream);
		return stream;
	}

	public override MemoryStream Create(byte[] bytes)
	{
		var stream = new TrackDisposeStream(bytes);
		Created.Add(stream);
		return stream;
	}

	public override MemoryStream Create(byte[] bytes, int index, int count)
	{
		var stream = new TrackDisposeStream(bytes, index, count);
		Created.Add(stream);
		return stream;
	}

	public void Reset() => Created = [];
}
