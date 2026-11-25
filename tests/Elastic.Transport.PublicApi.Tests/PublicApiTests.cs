// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using AwesomeAssertions;
using Elastic.Transport;
using Elastic.Transport.VirtualizedCluster.Components;
using Nullean.Snapshots.PublicApi;
using Xunit;

namespace Elastic.Transport.PublicApi.Tests;

/// <summary>
/// The public API of the libraries as net10.0 sees it, checked against the committed snapshots in <c>.snapshots/</c>.
/// A change to it fails the test until the new snapshot is accepted (<c>snap accept</c>); the <c>api/</c> folder holds
/// every target framework and is checked in CI by <c>./build.sh verifyapi</c>.
/// </summary>
public class PublicApiTests
{
	[Fact]
	public void TransportPublicApiIsUnchanged() => typeof(ITransport).Assembly.Should().MatchApiSnapshot();

	[Fact]
	public void VirtualizedClusterPublicApiIsUnchanged() =>
		typeof(VirtualCluster).Assembly.Should().MatchApiSnapshot();
}
