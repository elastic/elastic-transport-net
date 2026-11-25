// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using CsCheck;
using Elastic.Transport.Products.Elasticsearch;
using FluentAssertions;
using Xunit;

namespace Elastic.Transport.Properties.Tests;

/// <summary>
/// What a sniff makes of a _nodes response: which nodes it keeps, and the address, name, id and features of each. The model is
/// the documented shape of the API: a node that serves HTTP is kept, its address is publish_address or else the first bound
/// address, shown by its host name when there is one, and its roles say what it can do.
/// </summary>
public class SniffParsingProperties
{
	private static readonly Gen<string> Label =
		Gen.Int[0, 35].Select(i => "abcdefghijklmnopqrstuvwxyz0123456789"[i]).Array[1, 10].Select(c => new string(c));

	private static readonly string[] AllRoles = ["master", "data", "ingest", "ml", "transform", "remote_cluster_client"];

	private sealed record NodeSpec(string Id, string Name, string[] Roles, string? Fqdn, string Ip, int Port, bool Ipv6, string AddressField, bool HttpBlock, bool? HttpEnabledSetting)
	{
		/// <summary>An address the way the API writes it: ip:port, fqdn/ip:port, or [ipv6]:port.</summary>
		public string Address => (Fqdn is null ? "" : Fqdn + "/") + (Ipv6 ? "[" + Ip + "]" : Ip) + ":" + Port;

		/// <summary>
		/// Kept when it serves HTTP (an explicit http.enabled setting wins, else having an http block) and says where: the address
		/// lives in the http block, so a node without one is skipped whatever its setting says.
		/// </summary>
		public bool Kept => (HttpEnabledSetting ?? HttpBlock) && HttpBlock;

		public static readonly Gen<NodeSpec> Generator =
			from id in Label
			from name in Label
			from roles in Gen.Bool.Array[AllRoles.Length].Select(m => AllRoles.Where((_, i) => m[i]).ToArray())
			from fqdn in Gen.Frequency((1, Gen.Const(default(string))), (1, Label.Select(l => (string?)(l + ".example.com"))))
			from ipv6 in Gen.Bool
			from octets in Gen.Int[1, 254].Array[4]
			from port in Gen.Int[1, 65535]
			from field in Gen.OneOf("publish_address", "bound_address")
			from httpBlock in Gen.Frequency((6, Gen.Const(true)), (1, Gen.Const(false)))
			from httpSetting in Gen.Frequency((5, Gen.Const(default(bool?))), (1, Gen.Bool.Select(b => (bool?)b)))
			select new NodeSpec(id, name, roles, fqdn, ipv6 ? "fe80::1" : string.Join(".", octets), port, ipv6, field, httpBlock, httpSetting);

		public JsonObject ToJson()
		{
			var node = new JsonObject
			{
				["name"] = Name,
				["version"] = "8.15.0",
				["roles"] = new JsonArray(Roles.Select(r => (JsonNode?)r).ToArray()),
			};
			if (HttpBlock)
			{
				node["http"] = AddressField == "publish_address"
					? new JsonObject { ["publish_address"] = Address }
					: new JsonObject { ["bound_address"] = new JsonArray((JsonNode?)Address) };
			}
			if (HttpEnabledSetting is { } enabled)
				node["settings"] = new JsonObject { ["http.enabled"] = enabled };
			return node;
		}
	}

	private static IReadOnlyCollection<Node> Sniff(IReadOnlyList<NodeSpec> specs, bool ssl)
	{
		var root = new JsonObject { ["cluster_name"] = "test", ["nodes"] = new JsonObject() };
		foreach (var spec in specs)
			root["nodes"]![spec.Id] = spec.ToJson();

		var invoker = new InMemoryRequestInvoker(Encoding.UTF8.GetBytes(root.ToJsonString()));
		var registration = ElasticsearchProductRegistration.Default;
		using var settings = new TransportConfiguration(new SingleNodePool(new Uri("http://localhost:9200")), invoker, productRegistration: registration);
		var bound = new BoundConfiguration(settings);
		var endpoint = registration.CreateSniffEndpoint(new Node(new Uri("http://localhost:9200")), new RequestConfiguration(), settings);

		return registration.Sniff(invoker, ssl, endpoint, bound).Item2;
	}

	// Distinct ids, as the API's are (they are the keys of the nodes object).
	private static readonly Gen<NodeSpec[]> Specs = NodeSpec.Generator.Array[0, 6].Where(a => a.Select(n => n.Id).Distinct().Count() == a.Length);

	[Fact]
	public void ASniffKeepsExactlyTheNodesThatServeHttpWithTheirAddressNameAndId() =>
		Gen.Select(Specs, Gen.Bool).Sample((specs, ssl) =>
		{
			var nodes = Sniff(specs, ssl);
			var kept = specs.Where(s => s.Kept).ToList();

			_ = nodes.Select(n => n.Id).Should().BeEquivalentTo(kept.Select(s => s.Id));
			foreach (var spec in kept)
			{
				var node = nodes.Single(n => n.Id == spec.Id);
				var host = spec.Fqdn ?? (spec.Ipv6 ? "[" + spec.Ip + "]" : spec.Ip);

				_ = node.Uri.Scheme.Should().Be(ssl ? "https" : "http");
				_ = node.Uri.Port.Should().Be(spec.Port, spec.Address);
				_ = node.Uri.Host.Trim('[', ']').Should().Be(host.Trim('[', ']'), spec.Address);
				_ = node.Name.Should().Be(spec.Name);
			}
		}, iter: 500);

	[Fact]
	public void ANodesRolesBecomeItsFeatures() =>
		Specs.Where(a => a.Any(n => n.Kept)).Sample(specs =>
		{
			foreach (var node in Sniff(specs, false))
			{
				var spec = specs.Single(s => s.Id == node.Id);

				_ = node.HasFeature(ElasticsearchNodeFeatures.MasterEligible).Should().Be(spec.Roles.Contains("master"), $"{spec.Id} {string.Join(",", spec.Roles)}");
				_ = node.HasFeature(ElasticsearchNodeFeatures.HoldsData).Should().Be(spec.Roles.Contains("data"));
				_ = node.HasFeature(ElasticsearchNodeFeatures.IngestEnabled).Should().Be(spec.Roles.Contains("ingest"));
			}
		}, iter: 300);

	[Fact]
	public void ANodeWithNoAddressAtAllIsSkippedNotAnError() =>
		Label.Sample(id =>
		{
			var root = new JsonObject { ["nodes"] = new JsonObject { [id] = new JsonObject { ["name"] = "n", ["http"] = new JsonObject() } } };
			var invoker = new InMemoryRequestInvoker(Encoding.UTF8.GetBytes(root.ToJsonString()));
			var registration = ElasticsearchProductRegistration.Default;
			using var settings = new TransportConfiguration(new SingleNodePool(new Uri("http://localhost:9200")), invoker, productRegistration: registration);
			var endpoint = registration.CreateSniffEndpoint(new Node(new Uri("http://localhost:9200")), new RequestConfiguration(), settings);

			_ = registration.Sniff(invoker, false, endpoint, new BoundConfiguration(settings)).Item2.Should().BeEmpty();
		}, iter: 50);
}
