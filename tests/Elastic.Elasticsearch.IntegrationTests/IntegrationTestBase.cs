// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.Elasticsearch.Xunit.XunitPlumbing;
using Elastic.Transport;
using Xunit.Abstractions;

namespace Elastic.Elasticsearch.IntegrationTests;

public abstract class IntegrationTestBase : IntegrationTestBase<DefaultCluster>
{
	protected IntegrationTestBase(DefaultCluster cluster, ITestOutputHelper output) : base(cluster, output) { }
}
public abstract class IntegrationTestBase<TCluster> : IClusterFixture<TCluster>
	where TCluster : DefaultCluster, new()
{
	protected TCluster Cluster { get; }
	protected DefaultHttpTransport Transport { get; }


	protected IntegrationTestBase(TCluster cluster, ITestOutputHelper output)
	{
		Cluster = cluster;
		Transport = cluster.CreateClient(output);
	}
}
