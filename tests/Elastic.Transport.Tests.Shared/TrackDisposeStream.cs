// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

namespace Elastic.Transport.Tests.Shared;

public class TrackDisposeStream : MemoryStream
{
	private readonly bool _canSeek;

	public TrackDisposeStream(bool canSeek = true) : base() => _canSeek = canSeek;

	public TrackDisposeStream(byte[] bytes, bool canSeek = true) : base(bytes) => _canSeek = canSeek;

	public TrackDisposeStream(byte[] bytes, int index, int count, bool canSeek = true) : base(bytes, index, count) => _canSeek = canSeek;

	public override bool CanSeek => _canSeek;

	public bool IsDisposed { get; private set; }

	protected override void Dispose(bool disposing)
	{
		IsDisposed = true;
		base.Dispose(disposing);
	}
}
