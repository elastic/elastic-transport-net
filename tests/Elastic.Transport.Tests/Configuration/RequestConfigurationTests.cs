// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using FluentAssertions;
using Xunit;
#if !NETFRAMEWORK
using Soenneker.Utils.AutoBogus;
#endif

namespace Elastic.Transport.Tests.Configuration;

public class RequestConfigurationTests
{
	[Fact]
	public void CopiesAllDefaults()
	{
		var config = new RequestConfiguration();
		var newConfig = new RequestConfiguration(config);

		config.Should().BeEquivalentTo(newConfig);
	}

	[Fact]
	public void SameDefaults()
	{
		IRequestConfiguration config = new RequestConfiguration();
		IRequestConfiguration newConfig = new RequestConfigurationDescriptor();

		config.Should().BeEquivalentTo(newConfig);
	}

#if !NETFRAMEWORK
	[Fact]
	public void CopiesAllProperties()
	{
		var autoFaker = new AutoFaker<RequestConfiguration>();
		autoFaker.RuleFor(x => x.ClientCertificates, f => new X509CertificateCollection());

		var config = autoFaker.Generate();
		config.Accept.Should().NotBeEmpty();
		config.ClientCertificates.Should().NotBeNull();

		IRequestConfiguration newConfig = new RequestConfiguration(config);
		config.Should().BeEquivalentTo(newConfig);

		IRequestConfiguration newDescriptor = new RequestConfigurationDescriptor(config);
		config.Should().BeEquivalentTo(newDescriptor);
	}
#endif
}
