// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.Elasticsearch.Ephemeral;
using Elastic.Elasticsearch.Xunit;
using Elastic.Transport;
using Elastic.Transport.Products.Elasticsearch;
using Xunit;
using Xunit.Abstractions;
using static Elastic.Elasticsearch.Ephemeral.ClusterAuthentication;

[assembly: TestFramework("Elastic.Elasticsearch.Xunit.Sdk.ElasticTestFramework", "Elastic.Elasticsearch.Xunit")]

namespace Elastic.Elasticsearch.IntegrationTests;

/// <summary> Declare our cluster that we want to inject into our test classes </summary>
public class DefaultCluster : XunitClusterBase
{
	protected static string Version = "8.7.0";

	public DefaultCluster() : this(new XunitClusterConfiguration(Version) { StartingPortNumber = 9202, AutoWireKnownProxies = true }) { }

	public DefaultCluster(XunitClusterConfiguration xunitClusterConfiguration) : base(xunitClusterConfiguration) { }

	public DefaultHttpTransport CreateClient(ITestOutputHelper output) =>
		this.GetOrAddClient(cluster =>
		{
			var nodes = NodesUris();
			var connectionPool = new StaticNodePool(nodes);
			var settings = new TransportConfiguration(connectionPool, productRegistration: ElasticsearchProductRegistration.Default)
				.Proxy(new Uri("http://localhost:8080"))
				.RequestTimeout(TimeSpan.FromSeconds(5))
				.ServerCertificateValidationCallback(CertificateValidations.AllowAll)
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

			return new DefaultHttpTransport(settings);
		});
}

public class SecurityCluster : DefaultCluster
{
	public SecurityCluster() : base(new XunitClusterConfiguration(Version, ClusterFeatures.Security | ClusterFeatures.SSL | ClusterFeatures.XPack)
	{
		StartingPortNumber = 9202
		//, TrialMode = XPackTrialMode.Trial
	}) { }
}
