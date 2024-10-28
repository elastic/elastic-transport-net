// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Elastic.Transport.IntegrationTests.Plumbing;
using Elastic.Transport.Products.Elasticsearch;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Elastic.Transport.IntegrationTests.Http;

public class StreamResponseTests(TransportTestServer instance) : AssemblyServerTestsBase(instance)
{
	private const string Path = "/streamresponse";

	[Fact]
	public async Task StreamResponse_ShouldNotBeDisposed()
	{
		var nodePool = new SingleNodePool(Server.Uri);
		var config = new TransportConfiguration(nodePool, productRegistration: new ElasticsearchProductRegistration(typeof(Clients.Elasticsearch.ElasticsearchClient)));
		var transport = new DistributedTransport(config);

		var response = await transport.PostAsync<StreamResponse>(Path, PostData.String("{}"));

		// Ensure the stream is readable
		using var sr = new StreamReader(response.Body);
		_ = sr.ReadToEndAsync();
	}

	[Fact]
	public async Task StreamResponse_MemoryStreamShouldNotBeDisposed()
	{
		var nodePool = new SingleNodePool(Server.Uri);
		var memoryStreamFactory = new TrackMemoryStreamFactory();
		var config = new TransportConfiguration(nodePool, productRegistration: new ElasticsearchProductRegistration(typeof(Clients.Elasticsearch.ElasticsearchClient)))
			.MemoryStreamFactory(memoryStreamFactory)
			.DisableDirectStreaming(true);

		var transport = new DistributedTransport(config);

		_ = await transport.PostAsync<StreamResponse>(Path, PostData.String("{}"));

		var memoryStream = memoryStreamFactory.Created.Last();

		memoryStream.IsDisposed.Should().BeFalse();
	}

	[Fact]
	public async Task  StringResponse_MemoryStreamShouldBeDisposed()
	{
		var nodePool = new SingleNodePool(Server.Uri);
		var memoryStreamFactory = new TrackMemoryStreamFactory();
		var config = new TransportConfiguration(nodePool, productRegistration: new ElasticsearchProductRegistration(typeof(Clients.Elasticsearch.ElasticsearchClient)))
			.MemoryStreamFactory(memoryStreamFactory)
			.DisableDirectStreaming(true);

		var transport = new DistributedTransport(config);

		_ = await transport.PostAsync<StringResponse>(Path, PostData.String("{}"));

		var memoryStream = memoryStreamFactory.Created.Last();

		memoryStream.IsDisposed.Should().BeTrue();
	}

	private class TrackDisposeStream : MemoryStream
	{
		public TrackDisposeStream() { }

		public TrackDisposeStream(byte[] bytes) : base(bytes) { }

		public TrackDisposeStream(byte[] bytes, int index, int count) : base(bytes, index, count) { }

		public bool IsDisposed { get; private set; }

		protected override void Dispose(bool disposing)
		{
			IsDisposed = true;
			base.Dispose(disposing);
		}
	}

	private class TrackMemoryStreamFactory : MemoryStreamFactory
	{
		public IList<TrackDisposeStream> Created { get; } = [];

		public override MemoryStream Create()
		{
			var stream = new TrackDisposeStream();
			Created.Add(stream);
			return stream;
		}

		public override MemoryStream Create(byte[] bytes)
		{
			var stream = new TrackDisposeStream(bytes);
			Created.Add(stream);
			return stream;
		}

		public override MemoryStream Create(byte[] bytes, int index, int count)
		{
			var stream = new TrackDisposeStream(bytes, index, count);
			Created.Add(stream);
			return stream;
		}
	}
}

[ApiController, Route("[controller]")]
public class StreamResponseController : ControllerBase
{
	[HttpPost]
	public Task<JsonElement> Post([FromBody] JsonElement body) => Task.FromResult(body);
}
