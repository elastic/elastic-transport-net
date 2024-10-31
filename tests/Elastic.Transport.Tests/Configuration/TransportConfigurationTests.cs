// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using AutoBogus;
using FluentAssertions;
using Xunit;

namespace Elastic.Transport.Tests.Configuration;

public class TransportConfigurationTests
{
	[Fact]
	public void CopiesAllDefaults()
	{
		var config = new TransportConfiguration();
		var newConfig = new TransportConfiguration(config);

		config.Should().BeEquivalentTo(newConfig);
	}

#if !NETFRAMEWORK
	[Fact]
	public void CopiesAllProperties()
	{

		var faker = AutoFaker.Create(builder => {});

		var config = faker.Generate<TransportConfiguration>();
		var newConfig = new TransportConfiguration(config);

		config.Accept.Should().NotBeEmpty();
		config.ClientCertificates.Should().NotBeNull();

		config.Should().BeEquivalentTo(newConfig);
	}
#endif
}
