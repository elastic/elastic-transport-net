// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Elastic.Transport.Products;
using Elastic.Transport.Tests.Plumbing;
using FluentAssertions;
using Xunit;

namespace Elastic.Transport.Tests;

public class ResponseBuilderDisposeTests
{
	private readonly ITransportConfiguration _settings = InMemoryConnectionFactory.Create().DisableDirectStreaming(false);
	private readonly ITransportConfiguration _settingsDisableDirectStream = InMemoryConnectionFactory.Create().DisableDirectStreaming();

	[Fact]
	public async Task StreamResponseWithPotentialBody_StreamIsNotDisposed() =>
		await AssertResponse<StreamResponse>(false, expectedDisposed: false);

	[Fact]
	public async Task StreamResponseWithPotentialBodyAndDisableDirectStreaming_MemoryStreamIsNotDisposed() =>
		await AssertResponse<StreamResponse>(true, expectedDisposed: false);

	[Fact]
	public async Task ResponseWithPotentialBodyButInvalidMimeType_MemoryStreamIsDisposed() =>
		await AssertResponse<TestResponse>(true, mimeType: "application/not-valid", expectedDisposed: true);

	[Fact]
	public async Task ResponseWithPotentialBodyButSkippedStatusCode_MemoryStreamIsDisposed() =>
		await AssertResponse<TestResponse>(true, skipStatusCode: 200, expectedDisposed: true);

	[Fact]
	public async Task ResponseWithPotentialBodyButEmptyJson_MemoryStreamIsDisposed() =>
		await AssertResponse<TestResponse>(true, responseJson: "  ", expectedDisposed: true);

	[Fact]
	// NOTE: The empty string here hits a fast path in STJ which returns default if the stream length is zero.
	public async Task ResponseWithPotentialBodyButNullResponseDuringDeserialization_MemoryStreamIsDisposed() =>
		await AssertResponse<TestResponse>(true, responseJson: "", expectedDisposed: true);

	[Fact]
	public async Task ResponseWithPotentialBodyAndCustomResponseBuilder_MemoryStreamIsDisposed() =>
		await AssertResponse<TestResponse>(true, customResponseBuilder: new TestCustomResponseBuilder(), expectedDisposed: true);

	[Fact]
	// NOTE: We expect one memory stream factory creation when handling error responses
	public async Task ResponseWithPotentialBodyAndErrorResponse_StreamIsDisposed() =>
		await AssertResponse<TestResponse>(true, productRegistration: new TestProductRegistration(), expectedDisposed: true);

	[Fact]
	public async Task StringResponseWithPotentialBodyAndDisableDirectStreaming_MemoryStreamIsDisposed() =>
		await AssertResponse<StringResponse>(false, expectedDisposed: true, memoryStreamCreateExpected: 1);

	private async Task AssertResponse<T>(bool disableDirectStreaming, int statusCode = 200, HttpMethod httpMethod = HttpMethod.GET, int contentLength = 10,
		bool expectedDisposed = true, string mimeType = "application/json", string responseJson = "{}", int skipStatusCode = -1,
		CustomResponseBuilder customResponseBuilder = null, ProductRegistration productRegistration = null, int memoryStreamCreateExpected = -1)
			where T : TransportResponse, new()
	{
		ITransportConfiguration config;

		var memoryStreamFactory = new TrackMemoryStreamFactory();

		if (skipStatusCode > -1 )
			config = InMemoryConnectionFactory.Create(productRegistration)
				.DisableDirectStreaming(disableDirectStreaming)
				.SkipDeserializationForStatusCodes(skipStatusCode)
				.MemoryStreamFactory(memoryStreamFactory);
		else if (productRegistration is not null)
			config = InMemoryConnectionFactory.Create(productRegistration)
				.DisableDirectStreaming(disableDirectStreaming)
				.MemoryStreamFactory(memoryStreamFactory);
		else
			config = disableDirectStreaming ? _settingsDisableDirectStream : _settings;

		var endpoint = new Endpoint(new EndpointPath(httpMethod, "/"), new Node(new Uri("http://localhost:9200")));
		var requestData = new RequestData(config, null, customResponseBuilder);

		var stream = new TrackDisposeStream();

		if (!string.IsNullOrEmpty(responseJson))
		{
			stream.Write(Encoding.UTF8.GetBytes(responseJson), 0, responseJson.Length);
			stream.Position = 0;
		}

		var response = config.ProductRegistration.ResponseBuilder.ToResponse<T>(endpoint, requestData, null, null, statusCode, null, stream, mimeType, contentLength, null, null);

		response.Should().NotBeNull();

		memoryStreamFactory.Created.Count.Should().Be(memoryStreamCreateExpected > -1 ? memoryStreamCreateExpected : disableDirectStreaming ? 1 : 0);
		if (disableDirectStreaming)
		{
			var memoryStream = memoryStreamFactory.Created[0];
			memoryStream.IsDisposed.Should().Be(expectedDisposed);
		}

		// The latest implementation should never dispose the incoming stream and assumes the caller will handler disposal
		stream.IsDisposed.Should().Be(false);

		stream = new TrackDisposeStream();
		var ct = new CancellationToken();

		response = await config.ProductRegistration.ResponseBuilder.ToResponseAsync<T>(endpoint, requestData, null, null, statusCode, null, stream, null, contentLength, null, null,
			cancellationToken: ct);

		response.Should().NotBeNull();

		memoryStreamFactory.Created.Count.Should().Be(memoryStreamCreateExpected > -1 ? memoryStreamCreateExpected + 1 : disableDirectStreaming ? 2 : 0);
		if (disableDirectStreaming)
		{
			var memoryStream = memoryStreamFactory.Created[0];
			memoryStream.IsDisposed.Should().Be(expectedDisposed);
		}

		// The latest implementation should never dispose the incoming stream and assumes the caller will handler disposal
		stream.IsDisposed.Should().Be(false);
	}

	private class TestProductRegistration : ProductRegistration
	{
		public override string DefaultMimeType => "application/json";
		public override string Name => "name";
		public override string ServiceIdentifier => "id";
		public override bool SupportsPing => false;
		public override bool SupportsSniff => false;
		public override HeadersList ResponseHeadersToParse => [];
		public override MetaHeaderProvider MetaHeaderProvider => null;
		public override string ProductAssemblyVersion => "0.0.0";
		public override IReadOnlyDictionary<string, object> DefaultOpenTelemetryAttributes => new Dictionary<string, object>();
		public override IReadOnlyCollection<string> DefaultHeadersToParse() => [];
		public override bool HttpStatusCodeClassifier(HttpMethod method, int statusCode) => true;
		public override ResponseBuilder ResponseBuilder => new TestErrorResponseBuilder();

		public override Endpoint CreatePingEndpoint(Node node, IRequestConfiguration requestConfiguration) => throw new NotImplementedException();
		public override Task<TransportResponse> PingAsync(IRequestInvoker requestInvoker, Endpoint endpoint, RequestData pingData, CancellationToken cancellationToken) => throw new NotImplementedException();
		public override TransportResponse Ping(IRequestInvoker requestInvoker, Endpoint endpoint, RequestData pingData) => throw new NotImplementedException();
		public override Endpoint CreateSniffEndpoint(Node node, IRequestConfiguration requestConfiguration, ITransportConfiguration settings) => throw new NotImplementedException();
		public override Task<Tuple<TransportResponse, IReadOnlyCollection<Node>>> SniffAsync(IRequestInvoker requestInvoker, bool forceSsl, Endpoint endpoint, RequestData requestData, CancellationToken cancellationToken) => throw new NotImplementedException();
		public override Tuple<TransportResponse, IReadOnlyCollection<Node>> Sniff(IRequestInvoker requestInvoker, bool forceSsl, Endpoint endpoint, RequestData requestData) => throw new NotImplementedException();
		public override bool NodePredicate(Node node) => throw new NotImplementedException();
		public override Dictionary<string, object> ParseOpenTelemetryAttributesFromApiCallDetails(ApiCallDetails callDetails) => throw new NotImplementedException();
		public override int SniffOrder(Node node) => throw new NotImplementedException();
		public override bool TryGetServerErrorReason<TResponse>(TResponse response, out string reason) => throw new NotImplementedException();
	}

	private class TestError : ErrorResponse
	{
		public string MyError { get; set; }

		public override bool HasError() => true;
	}

	private class TestErrorResponseBuilder : DefaultResponseBuilder<TestError>
	{
		protected override void SetErrorOnResponse<TResponse>(TResponse response, TestError error)
		{
			// nothing to do in this scenario
		}

		protected override bool TryGetError(ApiCallDetails apiCallDetails, RequestData requestData, Stream responseStream, out TestError error)
		{
			error = new TestError();
			return true;
		}

		protected override bool RequiresErrorDeserialization(ApiCallDetails details, RequestData requestData) => true;
	}

	private class TestCustomResponseBuilder : CustomResponseBuilder
	{
		public override object DeserializeResponse(Serializer serializer, ApiCallDetails response, Stream stream) =>
			new TestResponse { ApiCallDetails = response };

		public override Task<object> DeserializeResponseAsync(Serializer serializer, ApiCallDetails response, Stream stream, CancellationToken ctx = default) =>
			Task.FromResult<object>(new TestResponse { ApiCallDetails = response });
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
