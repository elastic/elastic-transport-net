// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.Elasticsearch.Xunit.XunitPlumbing;
using Elastic.Transport;
using Xunit.Abstractions;

namespace Elastic.Elasticsearch.IntegrationTests;

public abstract class IntegrationTestBase(DefaultCluster cluster, ITestOutputHelper output) : IntegrationTestBase<DefaultCluster>(cluster, output)
{
}

public abstract class IntegrationTestBase<TCluster>(TCluster cluster, ITestOutputHelper output) : IClusterFixture<TCluster>
	where TCluster : DefaultCluster, new()
{
	protected TCluster Cluster { get; } = cluster;
	protected ITransport RequestHandler { get; } = cluster.CreateClient(output);
}
