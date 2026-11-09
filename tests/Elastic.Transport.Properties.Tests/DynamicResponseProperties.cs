// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Text;
using System.Text.Json.Nodes;
using CsCheck;
using FluentAssertions;
using Xunit;

namespace Elastic.Transport.Properties.Tests;

/// <summary>
/// DynamicResponse (1.6k lines, AOT-compatible, no downstream users that we can find) and JsonResponse expose the same path API
/// over the same body. For any JSON, they must read the same value at the same path. This is the net under that code: it says
/// whether any change to it altered what a path returns.
/// </summary>
public class DynamicResponseProperties
{
	private static readonly Gen<string> Text =
		Gen.Int[0x20, 0xD7FF].Select(i => (char)i).Array[0, 20].Select(c => new string(c));

	private static readonly Gen<JsonObject> Json =
		from i in Gen.Int
		from s in Text
		from b in Gen.Bool
		from d in Gen.Int[-100000, 100000].Select(n => n / 100.0)
		from nestedS in Text
		from nestedI in Gen.Int
		from deepB in Gen.Bool
		select new JsonObject
		{
			["i"] = i, ["s"] = s, ["b"] = b, ["d"] = d,
			["o"] = new JsonObject { ["s"] = nestedS, ["i"] = nestedI, ["deep"] = new JsonObject { ["b"] = deepB } },
		};

	private static (DynamicResponse Dynamic, JsonResponse Json) Both(JsonObject body)
	{
		var bytes = Encoding.UTF8.GetBytes(body.ToJsonString());
		using var settings = new TransportConfiguration(new SingleNodePool(new Uri("http://localhost:9200")), new InMemoryRequestInvoker(bytes));
		var transport = new DistributedTransport(settings);
		return (transport.Get<DynamicResponse>("/"), transport.Get<JsonResponse>("/"));
	}

	[Fact]
	public void BothResponseTypesReadTheSameValueAtTheSamePath() =>
		Json.Sample(body =>
		{
			var (dynamic, json) = Both(body);

			_ = dynamic.Get<int>("i").Should().Be(json.Get<int>("i"));
			_ = dynamic.Get<string>("s").Should().Be(json.Get<string>("s"));
			_ = dynamic.Get<bool>("b").Should().Be(json.Get<bool>("b"));
			_ = dynamic.Get<double>("d").Should().BeApproximately(json.Get<double>("d"), 1e-9);
			_ = dynamic.Get<string>("o.s").Should().Be(json.Get<string>("o.s"));
			_ = dynamic.Get<int>("o.i").Should().Be(json.Get<int>("o.i"));
			_ = dynamic.Get<bool>("o.deep.b").Should().Be(json.Get<bool>("o.deep.b"));
		}, iter: 300);

	[Fact]
	public void BothAgreeWithTheBodyThatWasSent() =>
		Json.Sample(body =>
		{
			var (dynamic, json) = Both(body);

			_ = dynamic.Get<int>("i").Should().Be(body["i"]!.GetValue<int>());
			_ = json.Get<string>("o.s").Should().Be(body["o"]!["s"]!.GetValue<string>());
			_ = dynamic.Get<string>("o.s").Should().Be(body["o"]!["s"]!.GetValue<string>());
		}, iter: 200);
}
