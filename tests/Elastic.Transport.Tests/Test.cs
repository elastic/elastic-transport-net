using System;
using System.Threading;
using System.Threading.Tasks;
using Elastic.Transport.Products;
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

		public void MinimalUsage()
		{
			var settings = new TransportConfiguration(new Uri("http://localhost:9200"));
			var transport = new Transport(settings);

			var response = transport.Get<StringResponse>("/");

			var headResponse = transport.Head("/");
		}

		public class MyClientConfiguration : TransportConfigurationBase<MyClientConfiguration>
		{
			public MyClientConfiguration(
				IConnectionPool connectionPool = null,
				IConnection connection = null,
				ITransportSerializer requestResponseSerializer = null,
				IProductRegistration productRegistration = null)
				: base(
					connectionPool ?? new SingleNodeConnectionPool(new Uri("http://default-endpoint.example"))
					, connection, requestResponseSerializer, productRegistration)
			{
			}

			private string _setting;
			public MyClientConfiguration NewSettings(string value) => Assign(value, (c, v) => _setting = v);
		}

		public class MyClientRequestPipeline : RequestPipeline<MyClientConfiguration>
		{
			public MyClientRequestPipeline(MyClientConfiguration configurationValues, IDateTimeProvider dateTimeProvider, IMemoryStreamFactory memoryStreamFactory, IRequestParameters requestParameters)
				: base(configurationValues, dateTimeProvider, memoryStreamFactory, requestParameters)
			{
			}
		}

		public void ExtendingConfiguration()
		{
			var clientConfiguration = new MyClientConfiguration()
				.NewSettings("some-value");
			var transport = new Transport<MyClientConfiguration>(clientConfiguration);




		}

	}
}
