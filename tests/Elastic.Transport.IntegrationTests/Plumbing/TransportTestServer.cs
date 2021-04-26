/*
 * Licensed to Elasticsearch B.V. under one or more contributor
 * license agreements. See the NOTICE file distributed with
 * this work for additional information regarding copyright
 * ownership. Elasticsearch B.V. licenses this file to you under
 * the Apache License, Version 2.0 (the "License"); you may
 * not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *    http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing,
 * software distributed under the License is distributed on an
 * "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
 * KIND, either express or implied.  See the License for the
 * specific language governing permissions and limitations
 * under the License.
 */

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Xunit;

[assembly: TestFramework("Xunit.Extensions.Ordering.TestFramework", "Xunit.Extensions.Ordering")]

namespace Elastic.Transport.IntegrationTests.Plumbing
{
	public interface ITransportTestServer
	{
		Uri Uri { get;  }

		Transport DefaultTransport { get;  }
	}

	public class TransportTestServer : TransportTestServer<DefaultStartup>
	{
		private static readonly bool RunningMitmProxy = Process.GetProcessesByName("mitmproxy").Any();
		private static readonly bool RunningFiddler = Process.GetProcessesByName("fiddler").Any();
		private static string Localhost => "127.0.0.1";
		public static string LocalOrProxyHost => RunningFiddler || RunningMitmProxy ? "ipv4.fiddler" : Localhost;
		public static TransportConfiguration RerouteToProxyIfNeeded(TransportConfiguration config)
		{
			if (!RunningMitmProxy) return config;

			return config.Proxy(new Uri("http://127.0.0.1:8080"), null, (string)null);
		}

	}

	public class TransportTestServer<TStartup> : ITransportTestServer, IDisposable, IAsyncDisposable, IAsyncLifetime
		where TStartup : class
	{
		private readonly IWebHost _host;
		private Uri _uri;
		private Transport _defaultTransport;

		public TransportTestServer()
		{
			var url = $"http://{TransportTestServer.LocalOrProxyHost}:0";

			var configuration =
				new ConfigurationBuilder()
					.AddInMemoryCollection(new Dictionary<string, string> { ["urls"] = url })
					.Build();

			_host =
				new WebHostBuilder()
					.UseKestrel()
					.UseConfiguration(configuration)
					.UseStartup<TStartup>()
					.Build();
		}

		public Uri Uri
		{
			get => _uri ?? throw new Exception($"{nameof(Uri)} is not available until {nameof(StartAsync)} is called");
			private set => _uri = value;
		}

		public Transport DefaultTransport
		{
			get => _defaultTransport ?? throw new Exception($"{nameof(DefaultTransport)} is not available until {nameof(StartAsync)} is called");
			private set => _defaultTransport = value;
		}

		public async Task<TransportTestServer<TStartup>> StartAsync(CancellationToken token = default)
		{
			await _host.StartAsync(token);
			var port = _host.GetServerPort();
			var url = $"http://{TransportTestServer.LocalOrProxyHost}:{port}";
			Uri = new Uri(url);
			DefaultTransport = CreateTransport(c => new Transport(c));
			return this;
		}

		public Transport CreateTransport(Func<TransportConfiguration, Transport> create) =>
			create(TransportTestServer.RerouteToProxyIfNeeded(new TransportConfiguration(Uri)));

		public void Dispose() => _host?.Dispose();

		public Task InitializeAsync() => StartAsync();

		Task IAsyncLifetime.DisposeAsync() => DisposeAsync().AsTask();


		public ValueTask DisposeAsync()
		{
			Dispose();
			return ValueTask.CompletedTask;
		}


	}
}
