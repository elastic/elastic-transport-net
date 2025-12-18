// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Linq;

<<<<<<< TODO: Unmerged change from project 'Elastic.Transport.Tests(net481)', Before:
namespace Elastic.Transport.Tests.Components.NodePool
{
	public class StaticNodePoolTests
	{
		[Fact]
		public void MultipleRequestsWhenOnlyASingleEndpointIsConfiguredAndTheEndpointIsUnavailableDoNotThrowAnException()
		{
			Node[] nodes = [new Uri("http://localhost:9200")];
			var pool = new StaticNodePool(nodes);
			var transport = new DistributedTransport(new TransportConfiguration(pool));

			var response = transport.Request<StringResponse>(HttpMethod.GET, "/", null, null);

			response.ApiCallDetails.SuccessOrKnownError.Should().BeFalse();
			response.ApiCallDetails.AuditTrail.Count.Should().Be(1);

			var audit = response.ApiCallDetails.AuditTrail.First();
			audit.Event.Should().Be(Diagnostics.Auditing.AuditEvent.BadRequest);
			audit.Node.FailedAttempts.Should().Be(1);
			audit.Node.IsAlive.Should().BeFalse();

			response = transport.Request<StringResponse>(HttpMethod.GET, "/", null, null);

			response.ApiCallDetails.SuccessOrKnownError.Should().BeFalse();

			var eventCount = 0;

			foreach (var a in response.ApiCallDetails.AuditTrail)
			{
				eventCount++;

				if (eventCount == 1)
				{
					a.Event.Should().Be(Diagnostics.Auditing.AuditEvent.AllNodesDead);
				}

				if (eventCount == 2)
				{
					a.Event.Should().Be(Diagnostics.Auditing.AuditEvent.Resurrection);
				}

				if (eventCount == 3)
				{
					a.Event.Should().Be(Diagnostics.Auditing.AuditEvent.BadRequest);
					audit.Node.FailedAttempts.Should().Be(2);
					audit.Node.IsAlive.Should().BeFalse();
				}
=======
namespace Elastic.Transport.Tests.Components.NodePool;

public class StaticNodePoolTests
{
	[Fact]
	public void MultipleRequestsWhenOnlyASingleEndpointIsConfiguredAndTheEndpointIsUnavailableDoNotThrowAnException()
	{
		Node[] nodes = [new Uri("http://localhost:9200")];
		var pool = new StaticNodePool(nodes);
		var transport = new DistributedTransport(new TransportConfiguration(pool));

		var response = transport.Request<StringResponse>(HttpMethod.GET, "/", null, null);

		_ = response.ApiCallDetails.SuccessOrKnownError.Should().BeFalse();
		_ = response.ApiCallDetails.AuditTrail.Count.Should().Be(1);

		var audit = response.ApiCallDetails.AuditTrail.First();
		_ = audit.Event.Should().Be(Diagnostics.Auditing.AuditEvent.BadRequest);
		_ = audit.Node.FailedAttempts.Should().Be(1);
		_ = audit.Node.IsAlive.Should().BeFalse();

		response = transport.Request<StringResponse>(HttpMethod.GET, "/", null, null);

		_ = response.ApiCallDetails.SuccessOrKnownError.Should().BeFalse();

		var eventCount = 0;

		foreach (var a in response.ApiCallDetails.AuditTrail)
		{
			eventCount++;

			if (eventCount == 1)
			{
				_ = a.Event.Should().Be(Diagnostics.Auditing.AuditEvent.AllNodesDead);
			}

			if (eventCount == 2)
			{
				_ = a.Event.Should().Be(Diagnostics.Auditing.AuditEvent.Resurrection);
			}

			if (eventCount == 3)
			{
				_ = a.Event.Should().Be(Diagnostics.Auditing.AuditEvent.BadRequest);
				_ = audit.Node.FailedAttempts.Should().Be(2);
				_ = audit.Node.IsAlive.Should().BeFalse();
>>>>>>> After
using FluentAssertions;
using Xunit;

namespace Elastic.Transport.Tests.Components.NodePool;

public class StaticNodePoolTests
{
	[Fact]
	public void MultipleRequestsWhenOnlyASingleEndpointIsConfiguredAndTheEndpointIsUnavailableDoNotThrowAnException()
	{
		Node[] nodes = [new Uri("http://localhost:9200")];
		var pool = new StaticNodePool(nodes);
		var transport = new DistributedTransport(new TransportConfiguration(pool));

		var response = transport.Request<StringResponse>(HttpMethod.GET, "/", null, null);

		_ = response.ApiCallDetails.SuccessOrKnownError.Should().BeFalse();
		_ = response.ApiCallDetails.AuditTrail.Count.Should().Be(1);

		var audit = response.ApiCallDetails.AuditTrail.First();
		_ = audit.Event.Should().Be(Diagnostics.Auditing.AuditEvent.BadRequest);
		_ = audit.Node.FailedAttempts.Should().Be(1);
		_ = audit.Node.IsAlive.Should().BeFalse();

		response = transport.Request<StringResponse>(HttpMethod.GET, "/", null, null);

		_ = response.ApiCallDetails.SuccessOrKnownError.Should().BeFalse();

		var eventCount = 0;

		foreach (var a in response.ApiCallDetails.AuditTrail)
		{
			eventCount++;

			if (eventCount == 1)
			{
				_ = a.Event.Should().Be(Diagnostics.Auditing.AuditEvent.AllNodesDead);
			}

			if (eventCount == 2)
			{
				_ = a.Event.Should().Be(Diagnostics.Auditing.AuditEvent.Resurrection);
			}

			if (eventCount == 3)
			{
				_ = a.Event.Should().Be(Diagnostics.Auditing.AuditEvent.BadRequest);
				_ = audit.Node.FailedAttempts.Should().Be(2);
				_ = audit.Node.IsAlive.Should().BeFalse();
			}
		}
	}
}
