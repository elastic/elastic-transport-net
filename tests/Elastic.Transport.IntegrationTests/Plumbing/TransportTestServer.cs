// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
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
	public interface HttpTransportTestServer
	{
		Uri Uri { get;  }

		ITransport DefaultRequestHandler { get;  }
	}

	public class TransportTestServer : TransportTestServer<DefaultStartup>
	{
		private static readonly bool RunningMitmProxy = Process.GetProcessesByName("mitmproxy").Any();
		private static readonly bool RunningFiddler = Process.GetProcessesByName("fiddler").Any();
		private static string Localhost => "127.0.0.1";
		public static string LocalOrProxyHost => RunningFiddler || RunningMitmProxy ? "ipv4.fiddler" : Localhost;
		public static TransportConfigurationDescriptor RerouteToProxyIfNeeded(TransportConfigurationDescriptor config)
		{
			if (!RunningMitmProxy) return config;

			return config with { ProxyAddress = "http://127.0.0.1:8080" };
		}

	}

	public class TransportTestServer<TStartup> : HttpTransportTestServer, IDisposable, IAsyncDisposable, IAsyncLifetime
		where TStartup : class
	{
		private readonly IWebHost _host;
		private Uri _uri;
		private ITransport _defaultRequestHandler;

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

		public ITransport DefaultRequestHandler
		{
			get => _defaultRequestHandler ?? throw new Exception($"{nameof(DefaultRequestHandler)} is not available until {nameof(StartAsync)} is called");
			private set => _defaultRequestHandler = value;
		}

		public async Task<TransportTestServer<TStartup>> StartAsync(CancellationToken token = default)
		{
			await _host.StartAsync(token);
			var port = _host.GetServerPort();
			var url = $"http://{TransportTestServer.LocalOrProxyHost}:{port}";
			Uri = new Uri(url);
			DefaultRequestHandler = CreateTransport(c => new DistributedTransport(c));
			return this;
		}

		public ITransport CreateTransport(Func<TransportConfigurationDescriptor, ITransport> create) =>
			create(TransportTestServer.RerouteToProxyIfNeeded(new TransportConfigurationDescriptor(Uri)));

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
