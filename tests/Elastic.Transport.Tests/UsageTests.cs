// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Elastic.Transport.Products;
using Elastic.Transport.Products.Elasticsearch;
using FluentAssertions;
using Xunit;

// ReSharper disable UnusedVariable
// ReSharper disable NotAccessedField.Local

namespace Elastic.Transport.Tests;

public class UsageTests
{
	private enum MyEnum
	{
		[EnumMember(Value = "different")]
		Value
	}

	[Fact]
	public void EnumsUseEnumMemberAttribute()
	{
		var pool = new StaticNodePool([new Node(new Uri("http://localhost:9200"))]);
		var requestInvoker = new InMemoryRequestInvoker();
		var serializer = LowLevelRequestResponseSerializer.Instance;
		var product = ElasticsearchProductRegistration.Default;

		var settings = new TransportConfiguration(pool, requestInvoker, serializer, product);
		var transport = new DistributedTransport<TransportConfiguration>(settings);

		var requestParameters = new DefaultRequestParameters
		{
			QueryString =
			new Dictionary<string, object> { { "enum", MyEnum.Value } }
		};
		var path = requestParameters.CreatePathWithQueryStrings("/", settings);
		var response = transport.Request<StringResponse>(new EndpointPath(HttpMethod.GET, path), null, null, null);

		_ = response.ApiCallDetails.Uri.Should().Be(new Uri("http://localhost:9200?enum=different"));
	}

	[Fact]
	public void TransportVersionIsSet()
	{
		var version = ReflectionVersionInfo.TransportVersion;
		_ = version.Should().NotBeNull();
	}

	[Fact]
	public void Usage()
	{
		var pool = new StaticNodePool([new Node(new Uri("http://localhost:9200"))]);
		var requestInvoker = new HttpRequestInvoker();
		var serializer = LowLevelRequestResponseSerializer.Instance;
		var product = ElasticsearchProductRegistration.Default;

		var settings = new TransportConfiguration(pool, requestInvoker, serializer, product);
		var transport = new DistributedTransport<TransportConfiguration>(settings);

		var response = transport.Request<StringResponse>(HttpMethod.GET, "/");
	}

	[Fact]
	public void MinimalUsage()
	{
		var settings = new TransportConfiguration(new Uri("http://localhost:9200"));
		var transport = new DistributedTransport(settings);

		var response = transport.Get<StringResponse>("/");

		var headResponse = transport.Head("/");
	}

	[Fact]
	public void MinimalElasticsearch()
	{
		var uri = new Uri("http://localhost:9200");
		var settings = new TransportConfiguration(uri, ElasticsearchProductRegistration.Default);
		var transport = new DistributedTransport(settings);

		var response = transport.Get<StringResponse>("/");

		var headResponse = transport.Head("/");
	}

	[Fact]
	public void MinimalUsageWithRequestParameters()
	{
		var settings = new TransportConfiguration(new Uri("http://localhost:9200"));
		var transport = new DistributedTransport(settings);

		var response = transport.Get<StringResponse>("/", new DefaultRequestParameters());

		var headResponse = transport.Head("/");
	}

	public class MyClientConfiguration(
		NodePool nodePool = null,
		IRequestInvoker transportCLient = null,
		Serializer requestResponseSerializer = null,
		ProductRegistration productRegistration = null) : TransportConfigurationDescriptorBase<MyClientConfiguration>(
			nodePool ?? new SingleNodePool(new Uri("http://default-endpoint.example"))
				, transportCLient, requestResponseSerializer, productRegistration)
	{
		private string _setting;
		public MyClientConfiguration NewSettings(string value) => Assign(value, (c, v) => _setting = v);
	}

	[Fact]
	public void ExtendingConfiguration()
	{
		var clientConfiguration = new MyClientConfiguration()
			.NewSettings("some-value");

		var transport = new DistributedTransport<MyClientConfiguration>(clientConfiguration);
	}
}
