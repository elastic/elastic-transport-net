using Elastic.Transport.Products.Elasticsearch;

var registration = new ElasticsearchProductRegistration(typeof(Elastic.Clients.Elasticsearch.ElasticsearchClient));

Console.WriteLine(registration.DefaultMimeType ?? "NOT SPECIFIED");
