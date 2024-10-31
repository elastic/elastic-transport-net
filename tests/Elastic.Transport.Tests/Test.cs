// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using Elastic.Transport.Products;
using Elastic.Transport.Products.Elasticsearch;

// ReSharper disable UnusedVariable
// ReSharper disable NotAccessedField.Local

namespace Elastic.Transport.Tests
{
	public class Test
	{
		public void Usage()
		{
			var pool = new StaticNodePool(new[] {new Node(new Uri("http://localhost:9200"))});
			var requestInvoker = new HttpRequestInvoker();
			var serializer = LowLevelRequestResponseSerializer.Instance;
			var product = ElasticsearchProductRegistration.Default;

			var settings = new TransportConfigurationDescriptor(pool, requestInvoker, serializer, product);
			var transport = new DistributedTransport<TransportConfigurationDescriptor>(settings);

			var response = transport.Request<StringResponse>(HttpMethod.GET, "/");
		}

		public void MinimalUsage()
		{
			var settings = new TransportConfigurationDescriptor(new Uri("http://localhost:9200"));
			var transport = new DistributedTransport(settings);

			var response = transport.Get<StringResponse>("/");

			var headResponse = transport.Head("/");
		}

		public void MinimalElasticsearch()
		{
			var uri = new Uri("http://localhost:9200");
			var settings = new TransportConfigurationDescriptor(uri, ElasticsearchProductRegistration.Default);
			var transport = new DistributedTransport(settings);

			var response = transport.Get<StringResponse>("/");

			var headResponse = transport.Head("/");
		}

		public void MinimalUsageWithRequestParameters()
		{
			var settings = new TransportConfigurationDescriptor(new Uri("http://localhost:9200"));
			var transport = new DistributedTransport(settings);

			var response = transport.Get<StringResponse>("/", new DefaultRequestParameters());

			var headResponse = transport.Head("/");
		}

		public class MyClientConfiguration : TransportConfigurationDescriptorBase<MyClientConfiguration>
		{
			public MyClientConfiguration(
				NodePool nodePool = null,
				IRequestInvoker transportCLient = null,
				Serializer requestResponseSerializer = null,
				ProductRegistration productRegistration = null)
				: base(
					nodePool ?? new SingleNodePool(new Uri("http://default-endpoint.example"))
					, transportCLient, requestResponseSerializer, productRegistration)
			{
			}

			private string _setting;
			public MyClientConfiguration NewSettings(string value) => Assign(value, (c, v) => _setting = v);
		}

		public void ExtendingConfiguration()
		{
			var clientConfiguration = new MyClientConfiguration()
				.NewSettings("some-value");

			var transport = new DistributedTransport<MyClientConfiguration>(clientConfiguration);
		}
	}
}
