// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.IO;

namespace Elastic.Transport;

internal class BufferedResponseHelpers
{
	public const int BufferSize = 81920;

	public static byte[] SwapStreams(ref Stream responseStream, ref MemoryStream ms, bool disposeOriginal = false)
	{
		var bytes = ms.ToArray();

		if (disposeOriginal)
			responseStream.Dispose();

		responseStream = ms;
		responseStream.Position = 0;
		return bytes;
	}
}
