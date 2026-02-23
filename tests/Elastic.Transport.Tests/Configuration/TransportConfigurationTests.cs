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

public class TransportConfigurationTests
{
	[Fact]
	public void CopiesAllDefaults()
	{
		var config = new TransportConfiguration();
		var newConfig = new TransportConfiguration(config);

		_ = config.Should().BeEquivalentTo(newConfig);
	}

	[Fact]
	public void SameDefaults()
	{
		ITransportConfiguration config = new TransportConfiguration();
		ITransportConfiguration newConfig = new TransportConfigurationDescriptor();

		_ = config.Should().BeEquivalentTo(newConfig, c => c
			.Excluding(p => p.BootstrapLock)
		);

		_ = config.BootstrapLock.CurrentCount.Should().Be(newConfig.BootstrapLock.CurrentCount);
	}

#if !NETFRAMEWORK
	[Fact]
	public void CopiesAllProperties()
	{
		var autoFaker = new AutoFaker<TransportConfiguration>();
		_ = autoFaker.RuleFor(x => x.BootstrapLock, f => new SemaphoreSlim(1, 1));
		_ = autoFaker.RuleFor(x => x.ClientCertificates, f => []);

		var config = autoFaker.Generate();
		_ = config.Accept.Should().NotBeEmpty();
		_ = config.ClientCertificates.Should().NotBeNull();

		ITransportConfiguration newConfig = new TransportConfiguration(config);
		_ = config.Should().BeEquivalentTo(newConfig);

		ITransportConfiguration newDescriptor = new TransportConfigurationDescriptor(config);
		_ = config.Should().BeEquivalentTo(newDescriptor);
	}
#endif
}
