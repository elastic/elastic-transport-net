// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.Elasticsearch.Ephemeral;
using Elastic.Elasticsearch.Managed;
using Elastic.Elasticsearch.Xunit;
using Elastic.Transport;
using Elastic.Transport.Products.Elasticsearch;
using Xunit;
using Xunit.Abstractions;
using static Elastic.Elasticsearch.Ephemeral.ClusterAuthentication;

[assembly: TestFramework("Elastic.Elasticsearch.Xunit.Sdk.ElasticTestFramework", "Elastic.Elasticsearch.Xunit")]

namespace Elastic.Elasticsearch.IntegrationTests;

/// <summary> Declare our cluster that we want to inject into our test classes </summary>
public class DefaultCluster(XunitClusterConfiguration xunitClusterConfiguration) : XunitClusterBase(xunitClusterConfiguration)
{
	public DefaultCluster() : this(new XunitClusterConfiguration(Version) { StartingPortNumber = 9202, AutoWireKnownProxies = true }) { }

	protected static string Version => "8.7.0";

	public ITransport CreateClient(ITestOutputHelper output) =>
		this.GetOrAddClient(cluster =>
		{
			var nodes = NodesUris();
			var nodePool = new StaticNodePool(nodes);
			var settings = new TransportConfigurationDescriptor(nodePool, productRegistration: ElasticsearchProductRegistration.Default)
				.RequestTimeout(TimeSpan.FromSeconds(5))
				.OnRequestCompleted(d =>
				{
					try
					{
						output.WriteLine(d.DebugInformation);
					}
					catch
					{
						// ignored
					}
				})
				.EnableDebugMode();
			if (ClusterConfiguration.Features.HasFlag(ClusterFeatures.Security))
				settings = settings.Authentication(new BasicAuthentication(Admin.Username, Admin.Password));
			if (cluster.DetectedProxy != DetectedProxySoftware.None)
				settings = settings.Proxy(new Uri("http://localhost:8080"));
			if (ClusterConfiguration.Features.HasFlag(ClusterFeatures.SSL))
				settings = settings.ServerCertificateValidationCallback(CertificateValidations.AllowAll);

			return new DistributedTransport(settings);
		});
}

public class SecurityCluster : DefaultCluster
{
	public SecurityCluster() : base(new XunitClusterConfiguration(Version, ClusterFeatures.Security | ClusterFeatures.SSL | ClusterFeatures.XPack)
	{
		StartingPortNumber = 9202
	})
	{ }
}
