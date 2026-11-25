// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Linq;
using System.Text;
using CsCheck;
using FluentAssertions;
using Xunit;

namespace Elastic.Transport.Properties.Tests;

/// <summary>A cloud id is cluster_name:base64(host[:port]$elasticsearch_id[:port][$kibana_id[:port]]). Which endpoint it names, as a model.</summary>
public class CloudIdProperties
{
	private static readonly Gen<string> Label =
		Gen.Int[0, 35].Select(i => "abcdefghijklmnopqrstuvwxyz0123456789"[i]).Array[1, 12].Select(c => new string(c));

	private static readonly Gen<string> HostName =
		Gen.Select(Label, Label, Gen.OneOf("com", "io", "net")).Select(t => $"{t.Item1}.{t.Item2}.{t.Item3}");

	private static readonly Gen<int?> PortChoice = Gen.Frequency((2, Gen.Const(default(int?))), (1, Gen.OneOf(443, 9243, 9200, 8443).Select(p => (int?)p)));

	private sealed record CloudId(string Name, string Host, int? HostPort, string EsId, int? EsPort, string? KibanaId, int? KibanaPort)
	{
		public static readonly Gen<CloudId> Generator =
			from name in Label
			from host in HostName
			from hostPort in PortChoice
			from es in Label
			from esPort in PortChoice
			from kibana in Gen.Frequency((1, Gen.Const(default(string))), (2, Label.Select(l => (string?)l)))
			from kibanaPort in PortChoice
			select new CloudId(name, host, hostPort, es, esPort, kibana, kibanaPort);

		public string Encode()
		{
			var parts = new System.Collections.Generic.List<string> { Host + (HostPort is { } hp ? $":{hp}" : ""), EsId + (EsPort is { } ep ? $":{ep}" : "") };
			if (KibanaId is not null)
				parts.Add(KibanaId + (KibanaPort is { } kp ? $":{kp}" : ""));
			return $"{Name}:{Convert.ToBase64String(Encoding.UTF8.GetBytes(string.Join("$", parts)))}";
		}

		/// <summary>The port an id uses: its own, else the host's, else 443; and 443 is left out of the address.</summary>
		private string Address(string id, int? idPort)
		{
			var port = idPort ?? HostPort ?? 443;
			return port == 443 ? $"https://{id}.{Host}/" : $"https://{id}.{Host}:{port}/";
		}

		public string ElasticsearchAddress => Address(EsId, EsPort);

		public string? KibanaAddress => KibanaId is null ? null : Address(KibanaId, KibanaPort);
	}

	[Fact]
	public void ACloudIdNamesTheElasticsearchEndpointItEncodes() =>
		CloudId.Generator.Sample(id =>
		{
			var pool = new CloudNodePool(id.Encode(), new BasicAuthentication("u", "p"));

			_ = pool.Nodes.Single().Uri.AbsoluteUri.Should().Be(id.ElasticsearchAddress, id.Encode());
		}, iter: 500);

	[Fact]
	public void TheSameCloudIdNamesTheKibanaEndpointWhenAskedFor() =>
		CloudId.Generator.Where(i => i.KibanaId is not null).Sample(id =>
		{
			var pool = new CloudNodePool(id.Encode(), new BasicAuthentication("u", "p"), CloudService.Kibana);

			_ = pool.Nodes.Single().Uri.AbsoluteUri.Should().Be(id.KibanaAddress, id.Encode());
		}, iter: 300);

	[Fact]
	public void AskingForKibanaWhenTheIdHasNoneIsAnError() =>
		CloudId.Generator.Where(i => i.KibanaId is null).Sample(id =>
			_ = ((Action)(() => _ = new CloudNodePool(id.Encode(), new BasicAuthentication("u", "p"), CloudService.Kibana))).Should().Throw<ArgumentException>(), iter: 100);

	[Fact]
	public void ACloudPoolIsOneNodeOverHttps() =>
		CloudId.Generator.Sample(id =>
		{
			var pool = new CloudNodePool(id.Encode(), new BasicAuthentication("u", "p"));

			_ = pool.UsingSsl.Should().BeTrue();
			_ = pool.Nodes.Should().HaveCount(1);
		}, iter: 200);
}
