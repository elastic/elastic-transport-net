// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;

namespace Elastic.Transport.Tests.Plumbing
{
	public static class InMemoryConnectionFactory
	{
		public static TransportConfiguration Create()
		{
			var invoker = new InMemoryRequestInvoker();
			var pool = new SingleNodePool(new Uri("http://localhost:9200"));
			var settings = new TransportConfiguration(pool, transport);
			return settings;
		}
	}
}
