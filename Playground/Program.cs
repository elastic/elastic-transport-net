// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.Transport;
using Elastic.Transport.Products.Elasticsearch;
using HttpMethod = Elastic.Transport.HttpMethod;

var registration = new ElasticsearchProductRegistration(typeof(Elastic.Clients.Elasticsearch.ElasticsearchClient));

var apiKey = Environment.GetEnvironmentVariable("ELASTIC_API_KEY") ?? throw new Exception();
var url = Environment.GetEnvironmentVariable("ELASTIC_URL") ?? throw new Exception();

var configuration = new TransportConfiguration(new Uri(url), new ApiKey(apiKey), ElasticsearchProductRegistration.Default)
{
	DebugMode = true
};
var transport = new DistributedTransport(configuration);

var response = transport.Request<EsResponse>(HttpMethod.GET, "/does-not-exist");
Console.WriteLine(response.DebugInformation);

var dynamicResponse = transport.Request<DynamicResponse>(HttpMethod.GET, "/");
Console.WriteLine(dynamicResponse.Body.Get<string>("version.build_flavor"));

var body = PostData.String(/*lang=json,strict*/ "{\"name\": \"test\"}");
var indexResponse = transport.Request<EsResponse>(HttpMethod.POST, "/does-not-exist/_doc", body);
Console.WriteLine(indexResponse.DebugInformation);

Console.WriteLine(registration.DefaultContentType ?? "NOT SPECIFIED");

internal sealed class EsResponse : ElasticsearchResponse;
