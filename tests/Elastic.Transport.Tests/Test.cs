using System;
using Elastic.Transport.Products.Elasticsearch;

namespace Elastic.Transport.Tests
{
	public class Test
	{
		public void Usage()
		{
			var pool = new StaticConnectionPool(new [] { new Node(new Uri("http://localhost:9200")) });
			var connection = new HttpConnection();
			var serializer = LowLevelRequestResponseSerializer.Instance;

			var product = ElasticsearchProductRegistration.Default;
			var settings = new TransportConfiguration(pool, connection, serializer, product);
			var transport = new Transport<TransportConfiguration>(settings);

			var response = transport.Request<StringResponse>(HttpMethod.GET, "/");
		}

	}
}
