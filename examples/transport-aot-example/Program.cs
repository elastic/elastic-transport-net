// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

#pragma warning disable CA1852

using System.Text.Json.Serialization;
using Elastic.Transport;
using Elastic.Transport.Products.Elasticsearch;

var apiKey = Environment.GetEnvironmentVariable("ELASTIC_API_KEY");
var url = Environment.GetEnvironmentVariable("ELASTIC_URL");

var configuration = apiKey is not null && url is not null
	? new ElasticsearchConfiguration(new Uri(url), new ApiKey(apiKey))
	: new ElasticsearchConfiguration { DebugMode = true };

var transport = new DistributedTransport(configuration);

var rootResponse = transport.Get<DynamicResponse>("/");
if (rootResponse.ApiCallDetails.HasSuccessfulStatusCode)
	Console.WriteLine(rootResponse.Get<string>("tagline"));
else
	Console.WriteLine(rootResponse);

internal sealed class MyDocument
{
	[JsonPropertyName("message")]
	public string Message { init; get; } = null!;
}

[JsonSerializable(typeof(MyDocument))]
internal partial class ExampleJsonSerializerContext : JsonSerializerContext;
