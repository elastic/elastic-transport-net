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
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Elastic.Transport.IntegrationTests.Plumbing
{
	public interface IHttpTransportTestServer
	{
		Uri Uri { get;  }

		ITransport DefaultRequestHandler { get;  }
	}

	public class TestServerFixture : TransportTestServer<DefaultStartup>;

	public abstract class TransportTestServer<TStartup> : IHttpTransportTestServer, IDisposable, IAsyncLifetime
		where TStartup : class
	{
		private readonly IHost _host;
		private readonly IServer _server;

		public TransportTestServer()
		{
			var url = $"http://127.0.0.1:0";

			var configuration =
				new ConfigurationBuilder()
					.AddInMemoryCollection(new Dictionary<string, string> { ["urls"] = url })
					.Build();

			_host =
				new HostBuilder()
					.ConfigureWebHost(builder =>
						builder.UseKestrel()
							.UseConfiguration(configuration)
							.UseStartup<TStartup>()
					)
				.Build();
			_server = _host.Services.GetRequiredService<IServer>();
		}

		public Uri Uri
		{
			get => field ?? throw new Exception($"{nameof(Uri)} is not available until {nameof(StartAsync)} is called");
			private set;
		}

		public ITransport DefaultRequestHandler
		{
			get => field ?? throw new Exception($"{nameof(DefaultRequestHandler)} is not available until {nameof(StartAsync)} is called");
			private set;
		}

		public async ValueTask StartAsync(CancellationToken token = default)
		{
			await _host.StartAsync(token);
			var port = _server.GetServerPort();
			var url = $"http://127.0.0.1:{port}";
			Uri = new Uri(url);
			DefaultRequestHandler = CreateTransport(c => new DistributedTransport(c));
		}

		public ITransport CreateTransport(Func<TransportConfiguration, ITransport> create) =>
			create(new TransportConfiguration(Uri));

		public void Dispose() => _host?.Dispose();

		public ValueTask InitializeAsync() => StartAsync();

		public ValueTask DisposeAsync()
		{
			Dispose();
			return ValueTask.CompletedTask;
		}

		/// <inheritdoc />
		public string FailureTestOutput() => string.Empty;

		/// <inheritdoc />
		public int? MaxConcurrency => 1;
	}
}
