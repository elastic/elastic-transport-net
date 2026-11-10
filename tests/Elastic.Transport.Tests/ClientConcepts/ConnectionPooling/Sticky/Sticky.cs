// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Elastic.Transport.VirtualizedCluster;
using Elastic.Transport.VirtualizedCluster.Audit;
using Elastic.Transport.VirtualizedCluster.Providers;
using Elastic.Transport.VirtualizedCluster.Rules;
using FluentAssertions;
using Xunit;
using static Elastic.Transport.Diagnostics.Auditing.AuditEvent;
using static Elastic.Transport.VirtualizedCluster.Rules.TimesHelper;

namespace Elastic.Transport.Tests.ClientConcepts.ConnectionPooling.Sticky;

public class Sticky
{

	[Fact]
	public void EachViewStartsAtNextPositionAndWrapsOver()
	{
		var numberOfNodes = 10;
		var uris = Enumerable.Range(9200, numberOfNodes).Select(p => new Uri("http://localhost:" + p));
		var pool = new StickyNodePool(uris);

		var startingPositions = Enumerable.Range(0, numberOfNodes)
			.Select(i => pool.CreateView(null).First())
			.Select(n => n.Uri.Port)
			.ToList();

		var expectedOrder = Enumerable.Repeat(9200, numberOfNodes);
		startingPositions.Should().Equal(expectedOrder);
	}

}
