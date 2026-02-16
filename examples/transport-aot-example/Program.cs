// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Elastic.Transport;
using Elastic.Transport.Products.Elasticsearch;

// --- Inline AOT smoke tests (no server needed) ---

// DynamicResponse inline test
var dict = new DynamicDictionary
{
	["name"] = new DynamicValue("test"),
	["count"] = new DynamicValue(42),
	["nested"] = new DynamicValue(new DynamicDictionary
	{
		["value"] = new DynamicValue("deep")
	})
};
var dr = new DynamicResponse(dict);
Assert(dr.Get<string>("name") == "test", "DynamicResponse Get<string>");
Assert(dr.Get<int>("count") == 42, "DynamicResponse Get<int>");
Assert(dr.Get<string>("nested.value") == "deep", "DynamicResponse nested Get<string>");

// JsonResponse inline test
var node = JsonNode.Parse("""{"hits":{"total":42,"hits":[{"_source":{"name":"test"}},{"_source":{"name":"last"}}]}}""")!;
var jr = new JsonResponse(node);
Assert(jr.Get<int>("hits.total") == 42, "JsonResponse Get<int>");
Assert(jr.Get<string>("hits.hits.[0]._source.name") == "test", "JsonResponse bracket index");
Assert(jr.Get<string>("hits.hits._first_._source.name") == "test", "JsonResponse _first_");
Assert(jr.Get<string>("hits.hits._last_._source.name") == "last", "JsonResponse _last_");
Assert(jr.Get<string>("hits.hits.[last()]._source.name") == "last", "JsonResponse [last()]");

// JsonResponse direct DOM access
Assert(jr.Body!["hits"]!["total"]!.GetValue<int>() == 42, "JsonResponse DOM access");
Assert(jr.Body["hits"]!["hits"]![0]!["_source"]!["name"]!.GetValue<string>() == "test", "JsonResponse DOM array access");

Console.WriteLine("All inline AOT smoke tests passed.");

// --- Server-based tests (only when configured) ---
var apiKey = Environment.GetEnvironmentVariable("ELASTIC_API_KEY");
var url = Environment.GetEnvironmentVariable("ELASTIC_URL");

var configuration = apiKey is not null && url is not null
	? new ElasticsearchConfiguration(new Uri(url), new ApiKey(apiKey))
	: new ElasticsearchConfiguration { DebugMode = true };

var transport = new DistributedTransport(configuration);

var rootResponse = transport.Get<DynamicResponse>("/");
if (rootResponse.ApiCallDetails.HasSuccessfulStatusCode)
	Console.WriteLine($"DynamicResponse tagline: {rootResponse.Get<string>("tagline")}");
else
	Console.WriteLine(rootResponse);

var jsonRootResponse = transport.Get<JsonResponse>("/");
if (jsonRootResponse.ApiCallDetails.HasSuccessfulStatusCode)
	Console.WriteLine($"JsonResponse tagline: {jsonRootResponse.Get<string>("tagline")}");
else
	Console.WriteLine(jsonRootResponse);

static void Assert(bool condition, string message)
{
	if (!condition) throw new Exception($"Assertion failed: {message}");
	Console.WriteLine($"  PASS: {message}");
}

public class MyDocument
{
	[JsonPropertyName("message")]
	public string Message { init; get; } = null!;
}

[JsonSerializable(typeof(MyDocument))]
internal partial class ExampleJsonSerializerContext : JsonSerializerContext;
