// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

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
		public static readonly ConcurrentQueue<int> PortNumbers = new(Enumerable.Range(3000, 100));
		public static readonly bool RunningMitmProxy = Process.GetProcessesByName("mitmproxy").Any();
		public static readonly bool RunningFiddler = Process.GetProcessesByName("fiddler").Any();
		private static string Localhost => "localhost";
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
		private readonly int _port;


		public TransportTestServer()
		{
			_port = TransportTestServer.PortNumbers.TryDequeue(out var p) ? p : throw new Exception("Failed to locate a portnumber");
			var url = $"http://{TransportTestServer.LocalOrProxyHost}:{_port}";
			Uri = new Uri(url);

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

			DefaultTransport = CreateTransport(c => new Transport(c));
		}
		public Uri Uri { get; }

		public Transport DefaultTransport { get; }

		public Transport CreateTransport(Func<TransportConfiguration, Transport> create) =>
			create(TransportTestServer.RerouteToProxyIfNeeded(new TransportConfiguration(Uri)));

		public async Task<TransportTestServer<TStartup>> StartAsync(CancellationToken token = default)
		{
			await _host.StartAsync(token);
			return this;
		}

		public void Dispose()
		{
			_host?.Dispose();
			TransportTestServer.PortNumbers.Enqueue(_port);
		}

		public Task InitializeAsync() => StartAsync();

		Task IAsyncLifetime.DisposeAsync() => DisposeAsync().AsTask();


		public ValueTask DisposeAsync()
		{
			Dispose();
			return ValueTask.CompletedTask;
		}


	}
}
