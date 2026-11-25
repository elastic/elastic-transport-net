// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using FluentAssertions;
using Xunit;

namespace Elastic.Transport.Tests;

/// <summary>
/// A configuration that a transport uses cannot be changed any more. Before, a change after that point was half applied:
/// the transport's own bound configuration had been copied already, while a per-request one read the live values.
/// </summary>
public class ConfigurationLockTests
{
	private static TransportConfigurationDescriptor Descriptor() => new(new Uri("http://localhost:9200"), null);

	[Fact]
	public void ADescriptorCanBeChangedUntilATransportUsesIt()
	{
		var descriptor = Descriptor();

		var returned = descriptor.MaximumRetries(3).RequestTimeout(TimeSpan.FromSeconds(5));

		_ = returned.Should().BeSameAs(descriptor);
		_ = ((ITransportConfiguration)descriptor).MaxRetries.Should().Be(3);
	}

	[Fact]
	public void ChangingADescriptorAfterATransportUsesItThrows()
	{
		var descriptor = Descriptor();
		_ = new DistributedTransport(descriptor);

		Action change = () => descriptor.MaximumRetries(3);

		_ = change.Should().Throw<InvalidOperationException>().WithMessage("*in use by a transport*");
	}

	[Fact]
	public void EveryDescriptorMethodChecks()
	{
		var descriptor = Descriptor();
		_ = new DistributedTransport(descriptor);

		_ = FluentActions.Invoking(() => descriptor.ThrowExceptions()).Should().Throw<InvalidOperationException>();
		_ = FluentActions.Invoking(() => descriptor.EnableHttpCompression(false)).Should().Throw<InvalidOperationException>();
		_ = FluentActions.Invoking(() => descriptor.GlobalHeaders(new System.Collections.Specialized.NameValueCollection { { "x", "y" } })).Should().Throw<InvalidOperationException>();
		_ = FluentActions.Invoking(() => descriptor.OnRequestCompleted(_ => { })).Should().Throw<InvalidOperationException>();
	}

	[Fact]
	public void ASubclassUsingTheProtectedHelperIsLockedToo()
	{
		var descriptor = new Derived(new SingleNodePool(new Uri("http://localhost:9200")));
		_ = descriptor.Mark("before").Should().BeSameAs(descriptor);
		_ = new DistributedTransport(descriptor);

		_ = FluentActions.Invoking(() => descriptor.Mark("after")).Should().Throw<InvalidOperationException>();
	}

	[Fact]
	public void ACopyOfALockedConfigurationCanBeChanged()
	{
		var descriptor = Descriptor();
		_ = new DistributedTransport(descriptor);

		var copy = new TransportConfigurationDescriptor(descriptor).MaximumRetries(7);

		_ = ((ITransportConfiguration)copy).MaxRetries.Should().Be(7);
		_ = ((ITransportConfiguration)descriptor).MaxRetries.Should().BeNull("the original is untouched");
	}

	[Fact]
	public void TwoTransportsCanShareOneConfiguration()
	{
		var descriptor = Descriptor().MaximumRetries(2);

		_ = new DistributedTransport(descriptor);
		_ = new DistributedTransport(descriptor);
	}

	[Fact]
	public void ARecordConfigurationStaysImmutableAndCopiesWithWith()
	{
		var config = new TransportConfiguration(new Uri("http://localhost:9200"));
		_ = new DistributedTransport(config);

		var changed = config with { MaxRetries = 4 };

		_ = changed.MaxRetries.Should().Be(4);
		_ = config.MaxRetries.Should().BeNull();
	}

	private sealed class Derived(NodePool pool) : TransportConfigurationDescriptorBase<Derived>(pool, null, null, null)
	{
		public string Marked { get; private set; }

		public Derived Mark(string value) => Assign(value, static (a, v) => a.Marked = v);
	}
}
