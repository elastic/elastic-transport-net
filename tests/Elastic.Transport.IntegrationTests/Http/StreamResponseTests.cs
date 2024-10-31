// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
		var config = new TransportConfigurationDescriptor(nodePool, productRegistration: new ElasticsearchProductRegistration(typeof(Clients.Elasticsearch.ElasticsearchClient)));
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
		var config = new TransportConfigurationDescriptor(nodePool, productRegistration: new ElasticsearchProductRegistration(typeof(Clients.Elasticsearch.ElasticsearchClient)))
			.MemoryStreamFactory(memoryStreamFactory)
			.DisableDirectStreaming(true);

		var transport = new DistributedTransport(config);

		_ = await transport.PostAsync<StreamResponse>(Path, PostData.String("{}"));

		// When disable direct streaming, we have 1 for the original content, 1 for the buffered request bytes and the last for the buffered response
		memoryStreamFactory.Created.Count.Should().Be(3);
		memoryStreamFactory.Created.Last().IsDisposed.Should().BeFalse();
	}

	[Fact]
	public async Task StringResponse_MemoryStreamShouldBeDisposed()
	{
		var nodePool = new SingleNodePool(Server.Uri);
		var memoryStreamFactory = new TrackMemoryStreamFactory();
		var config = new TransportConfigurationDescriptor(nodePool, productRegistration: new ElasticsearchProductRegistration(typeof(Clients.Elasticsearch.ElasticsearchClient)))
			.MemoryStreamFactory(memoryStreamFactory);

		var transport = new DistributedTransport(config);

		_ = await transport.PostAsync<StringResponse>(Path, PostData.String("{}"));

		memoryStreamFactory.Created.Count.Should().Be(2);
		foreach (var memoryStream in memoryStreamFactory.Created)
		{
			memoryStream.IsDisposed.Should().BeTrue();
		}
	}

	[Fact]
	public async Task WhenInvalidJson_MemoryStreamShouldBeDisposed()
	{
		var nodePool = new SingleNodePool(Server.Uri);
		var memoryStreamFactory = new TrackMemoryStreamFactory();
		var config = new TransportConfigurationDescriptor(nodePool, productRegistration: new ElasticsearchProductRegistration(typeof(Clients.Elasticsearch.ElasticsearchClient)))
			.MemoryStreamFactory(memoryStreamFactory)
			.DisableDirectStreaming(true);

		var transport = new DistributedTransport(config);

		var payload = new Payload { ResponseJsonString = " " };
		_ = await transport.PostAsync<TestResponse>(Path, PostData.Serializable(payload));

		memoryStreamFactory.Created.Count.Should().Be(3);
		foreach (var memoryStream in memoryStreamFactory.Created)
		{
			memoryStream.IsDisposed.Should().BeTrue();
		}
	}

	[Fact]
	public async Task WhenNoContent_MemoryStreamShouldBeDisposed()
	{
		var nodePool = new SingleNodePool(Server.Uri);
		var memoryStreamFactory = new TrackMemoryStreamFactory();
		var config = new TransportConfigurationDescriptor(nodePool, productRegistration: new ElasticsearchProductRegistration(typeof(Clients.Elasticsearch.ElasticsearchClient)))
			.MemoryStreamFactory(memoryStreamFactory);

		var transport = new DistributedTransport(config);

		var payload = new Payload { ResponseJsonString = "", StatusCode = 204 };
		_ = await transport.PostAsync<TestResponse>(Path, PostData.Serializable(payload));

		// We expect one for sending the request payload, but as the response is 204, we shouldn't
		// see other memory streams being created for the response.
		memoryStreamFactory.Created.Count.Should().Be(2);
		foreach (var memoryStream in memoryStreamFactory.Created)
			memoryStream.IsDisposed.Should().BeTrue();
	}

	[Fact]
	public async Task PlainText_MemoryStreamShouldBeDisposed()
	{
		var nodePool = new SingleNodePool(Server.Uri);
		var memoryStreamFactory = new TrackMemoryStreamFactory();
		var config = new TransportConfigurationDescriptor(nodePool, productRegistration: new ElasticsearchProductRegistration(typeof(Clients.Elasticsearch.ElasticsearchClient)))
			.MemoryStreamFactory(memoryStreamFactory)
			.DisableDirectStreaming(true);

		var transport = new DistributedTransport(config);

		var payload = new Payload { ResponseJsonString = "text", ContentType = "text/plain" };
		_ = await transport.PostAsync<TestResponse>(Path, PostData.Serializable(payload));

		memoryStreamFactory.Created.Count.Should().Be(3);
		foreach (var memoryStream in memoryStreamFactory.Created)
		{
			memoryStream.IsDisposed.Should().BeTrue();
		}
	}

	private class TestResponse : TransportResponse
	{
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

public class Payload
{
	public string ResponseJsonString { get; set; } = "{}";
	public string ContentType { get; set; } = "application/json";
	public int StatusCode { get; set; } = 200;
}

[ApiController, Route("[controller]")]
public class StreamResponseController : ControllerBase
{
	[HttpPost]
	public async Task<ActionResult> Post([FromBody] Payload payload)
	{
		Response.ContentType = payload.ContentType;

		if (payload.StatusCode != 204)
		{
			await Response.BodyWriter.WriteAsync(Encoding.UTF8.GetBytes(payload.ResponseJsonString));
			await Response.BodyWriter.CompleteAsync();
		}

		return StatusCode(payload.StatusCode);
	}
}
