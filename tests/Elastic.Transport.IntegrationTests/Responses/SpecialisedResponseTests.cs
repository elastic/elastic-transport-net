// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Elastic.Transport.IntegrationTests.Plumbing;
using Elastic.Transport.Tests.Shared;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Elastic.Transport.IntegrationTests.Responses;

public class SpecialisedResponseTests(TransportTestServer instance) : AssemblyServerTestsBase(instance)
{
	private const string Path = "/specialresponse";
	private const string EmptyJson = "{}";
	private static readonly byte[] EmptyJsonBytes = [(byte)'{', (byte)'}'];

	// language=json
	private const string LargeJson = """
		[
		  {
		    "_id": "672b13c7666cae7721b7f5c8",
		    "index": 0,
		    "guid": "f8a9356c-660b-4f4f-a1c2-84048e0599b9",
		    "isActive": false,
		    "balance": "$3,856.23",
		    "picture": "http://placehold.it/32x32",
		    "age": 29,
		    "eyeColor": "green",
		    "name": "Noemi Reed",
		    "gender": "female",
		    "company": "LUNCHPOD",
		    "email": "noemireed@lunchpod.com",
		    "phone": "+1 (961) 417-3668",
		    "address": "954 Cameron Court, Onton, South Dakota, 1148",
		    "about": "Qui ad id veniam aute amet commodo officia est cillum. Elit nostrud Lorem tempor duis. Commodo velit nulla nisi velit laborum qui minim nostrud aute dolor tempor officia. Commodo proident nulla eu adipisicing incididunt eu. Quis nostrud Lorem amet deserunt pariatur ea elit adipisicing qui. Voluptate exercitation id esse tempor occaecat.\r\n",
		    "registered": "2017-02-28T04:33:12 -00:00",
		    "latitude": 30.32678,
		    "longitude": -156.977981,
		    "tags": [
		      "sit",
		      "culpa",
		      "cillum",
		      "labore",
		      "in",
		      "labore",
		      "quis"
		    ],
		    "friends": [
		      {
		        "id": 0,
		        "name": "Good Lyons"
		      },
		      {
		        "id": 1,
		        "name": "Mccarthy Delaney"
		      },
		      {
		        "id": 2,
		        "name": "Winters Combs"
		      }
		    ],
		    "greeting": "Hello, Noemi Reed! You have 8 unread messages.",
		    "favoriteFruit": "strawberry"
		  },
		  {
		    "_id": "672b13c741693abd9d0173a9",
		    "index": 1,
		    "guid": "fa3d27ec-213c-4365-92e9-39774eec9d01",
		    "isActive": false,
		    "balance": "$2,275.63",
		    "picture": "http://placehold.it/32x32",
		    "age": 23,
		    "eyeColor": "brown",
		    "name": "Cooley Williams",
		    "gender": "male",
		    "company": "GALLAXIA",
		    "email": "cooleywilliams@gallaxia.com",
		    "phone": "+1 (961) 439-2700",
		    "address": "791 Montgomery Place, Garfield, Guam, 9900",
		    "about": "Officia consectetur do quis id cillum quis esse. Aliqua deserunt eiusmod laboris cupidatat enim commodo est Lorem id nisi mollit non. Eiusmod adipisicing pariatur culpa nostrud incididunt dolor commodo fugiat amet ex dolor ex. Nostrud incididunt consequat ullamco pariatur cupidatat nulla eu voluptate cupidatat nulla. Mollit est id adipisicing ad mollit exercitation. Ullamco non ad aliquip ea sit culpa pariatur commodo veniam. In occaecat et tempor ea Lorem eu incididunt sit commodo officia.\r\n",
		    "registered": "2019-05-25T11:41:44 -01:00",
		    "latitude": -85.996713,
		    "longitude": -140.910029,
		    "tags": [
		      "esse",
		      "qui",
		      "magna",
		      "et",
		      "irure",
		      "est",
		      "in"
		    ],
		    "friends": [
		      {
		        "id": 0,
		        "name": "Pamela Castillo"
		      },
		      {
		        "id": 1,
		        "name": "Suzanne Herman"
		      },
		      {
		        "id": 2,
		        "name": "Gonzales Bush"
		      }
		    ],
		    "greeting": "Hello, Cooley Williams! You have 8 unread messages.",
		    "favoriteFruit": "apple"
		  }
		]
		""";

	[Fact]
	public async Task VoidResponse_ShouldReturnExpectedResponse()
	{
		var nodePool = new SingleNodePool(Server.Uri);
		var memoryStreamFactory = new TrackingMemoryStreamFactory();
		var config = new TransportConfiguration(nodePool)
		{
			EnableHttpCompression = false,
			MemoryStreamFactory = memoryStreamFactory
		};
		var transport = new DistributedTransport(config);

		var payload = new Payload { ResponseString = EmptyJson, StatusCode = 200 };

		var response = await transport.PostAsync<VoidResponse>(Path, PostData.Serializable(payload));

		Validate(memoryStreamFactory, response);

		memoryStreamFactory.Reset();
		response = transport.Post<VoidResponse>(Path, PostData.Serializable(payload));

		Validate(memoryStreamFactory, response);

		static void Validate(TrackingMemoryStreamFactory memoryStreamFactory, VoidResponse response)
		{
			response.Body.Should().BeSameAs(VoidResponse.Default.Body);
			response.ApiCallDetails.ResponseBodyInBytes.Should().BeNull();
			memoryStreamFactory.Created.Count.Should().Be(1); // One required for setting request content
		}
	}

	[Fact]
	public async Task DynamicResponse_WhenContentIsJson_AndNotDisableDirectStreaming_ShouldReturnExpectedResponse()
	{
		var nodePool = new SingleNodePool(Server.Uri);
		var memoryStreamFactory = new TrackingMemoryStreamFactory();
		var config = new TransportConfiguration(nodePool)
		{
			EnableHttpCompression = false,
			MemoryStreamFactory = memoryStreamFactory
		};
		var transport = new DistributedTransport(config);

		var json = "{\"propertyOne\":\"value1\",\"propertyTwo\":100}";
		var payload = new Payload { ResponseString = json, StatusCode = 200 };

		var response = await transport.PostAsync<DynamicResponse>(Path, PostData.Serializable(payload));

		Validate(memoryStreamFactory, response);

		memoryStreamFactory.Reset();
		response = transport.Post<DynamicResponse>(Path, PostData.Serializable(payload));

		Validate(memoryStreamFactory, response);

		static void Validate(TrackingMemoryStreamFactory memoryStreamFactory, DynamicResponse response)
		{
			response.Body.Should().BeOfType<DynamicDictionary>();
			response.Body.Values.Count.Should().Be(2);
			response.Body.Get<string>("propertyOne").Should().Be("value1");
			response.Body.Get<int>("propertyTwo").Should().Be(100);

			response.ApiCallDetails.ResponseBodyInBytes.Should().BeNull();
			memoryStreamFactory.Created.Count.Should().Be(1); // One required for setting request content
		}
	}

	[Fact]
	public async Task DynamicResponse_WhenContentIsNotJson_AndContentIsChunked_AndNotDisableDirectStreaming_ShouldReturnExpectedResponse()
	{
		var nodePool = new SingleNodePool(Server.Uri);
		var memoryStreamFactory = new TrackingMemoryStreamFactory();
		var config = new TransportConfiguration(nodePool)
		{
			EnableHttpCompression = false,
			MemoryStreamFactory = memoryStreamFactory
		};
		var transport = new DistributedTransport(config);

		var stringValue = "this is a string";
		var payload = new Payload { ResponseString = stringValue, StatusCode = 200, ContentType = "text/plain", IsChunked = true };

		var requestConfig = new RequestConfiguration { Accept = "text/plain" };
		var response = await transport.RequestAsync<DynamicResponse>(new EndpointPath(HttpMethod.POST, Path), PostData.Serializable(payload), default, requestConfig);

		Validate(memoryStreamFactory, response, stringValue);

		memoryStreamFactory.Reset();
		response = transport.Request<DynamicResponse>(new EndpointPath(HttpMethod.POST, Path), PostData.Serializable(payload), default, requestConfig);

		Validate(memoryStreamFactory, response, stringValue);

		static void Validate(TrackingMemoryStreamFactory memoryStreamFactory, DynamicResponse response, string expected)
		{
			response.Body.Should().BeOfType<DynamicDictionary>();
			response.Body.Values.Count.Should().Be(1);
			response.Body.Get<string>("body").Should().Be(expected);

			response.ApiCallDetails.ResponseBodyInBytes.Should().BeNull();
			memoryStreamFactory.Created.Count.Should().Be(1); // One required for setting request content
		}
	}

	[Fact]
	public async Task DynamicResponse_WhenContentIsJson_AndDisableDirectStreaming_ShouldReturnExpectedResponse()
	{
		var nodePool = new SingleNodePool(Server.Uri);
		var memoryStreamFactory = new TrackingMemoryStreamFactory();
		var config = new TransportConfiguration(nodePool)
		{
			EnableHttpCompression = false,
			DisableDirectStreaming = true,
			MemoryStreamFactory = memoryStreamFactory
		};
		var transport = new DistributedTransport(config);

		var json = "{\"propertyOne\":\"value1\",\"propertyTwo\":100}";
		var payload = new Payload { ResponseString = json, StatusCode = 200 };

		var response = await transport.PostAsync<DynamicResponse>(Path, PostData.Serializable(payload));

		Validate(memoryStreamFactory, response);

		memoryStreamFactory.Reset();
		response = transport.Post<DynamicResponse>(Path, PostData.Serializable(payload));

		Validate(memoryStreamFactory, response);

		static void Validate(TrackingMemoryStreamFactory memoryStreamFactory, DynamicResponse response)
		{
			response.Body.Should().BeOfType<DynamicDictionary>();
			response.Body.Values.Count.Should().Be(2);
			response.Body.Get<string>("propertyOne").Should().Be("value1");
			response.Body.Get<int>("propertyTwo").Should().Be(100);

			response.ApiCallDetails.ResponseBodyInBytes.Should().NotBeNull();
			memoryStreamFactory.Created.Count.Should().Be(3);
		}
	}

	[Fact]
	public async Task DynamicResponse_WhenContentIsNotJson_AndNotDisableDirectStreaming_ShouldReturnExpectedResponse()
	{
		var nodePool = new SingleNodePool(Server.Uri);
		var memoryStreamFactory = new TrackingMemoryStreamFactory();
		var config = new TransportConfiguration(nodePool)
		{
			EnableHttpCompression = false,
			MemoryStreamFactory = memoryStreamFactory
		};
		var transport = new DistributedTransport(config);

		var stringValue = "this is a string";
		var payload = new Payload { ResponseString = stringValue, StatusCode = 200, ContentType = "text/plain" };

		var requestConfig = new RequestConfiguration { Accept = "text/plain" };
		var response = await transport.RequestAsync<DynamicResponse>(new EndpointPath(HttpMethod.POST, Path), PostData.Serializable(payload), default, requestConfig);

		Validate(memoryStreamFactory, response, stringValue);

		memoryStreamFactory.Reset();
		response = transport.Request<DynamicResponse>(new EndpointPath(HttpMethod.POST, Path), PostData.Serializable(payload), default, requestConfig);

		Validate(memoryStreamFactory, response, stringValue);

		static void Validate(TrackingMemoryStreamFactory memoryStreamFactory, DynamicResponse response, string expected)
		{
			response.Body.Should().BeOfType<DynamicDictionary>();
			response.Body.Values.Count.Should().Be(1);
			response.Body.Get<string>("body").Should().Be(expected);

			response.ApiCallDetails.ResponseBodyInBytes.Should().BeNull();
			memoryStreamFactory.Created.Count.Should().Be(1); // One required for setting request content
		}
	}

	[Fact]
	public async Task DynamicResponse_WhenContentIsNotJson_AndDisableDirectStreaming_ShouldReturnExpectedResponse()
	{
		var nodePool = new SingleNodePool(Server.Uri);
		var memoryStreamFactory = new TrackingMemoryStreamFactory();
		var config = new TransportConfiguration(nodePool)
		{
			EnableHttpCompression = false,
			DisableDirectStreaming = true,
			MemoryStreamFactory = memoryStreamFactory
		};
		var transport = new DistributedTransport(config);

		var stringValue = "this is a string";
		var payload = new Payload { ResponseString = stringValue, StatusCode = 200, ContentType = "text/plain" };

		var requestConfig = new RequestConfiguration { Accept = "text/plain" };
		var response = await transport.RequestAsync<DynamicResponse>(new EndpointPath(HttpMethod.POST, Path), PostData.Serializable(payload), default, requestConfig);

		Validate(memoryStreamFactory, response);

		memoryStreamFactory.Reset();
		response = transport.Request<DynamicResponse>(new EndpointPath(HttpMethod.POST, Path), PostData.Serializable(payload), default, requestConfig);

		Validate(memoryStreamFactory, response);

		void Validate(TrackingMemoryStreamFactory memoryStreamFactory, DynamicResponse response)
		{
			response.Body.Should().BeOfType<DynamicDictionary>();
			response.Body.Values.Count.Should().Be(1);
			response.Body.Get<string>("body").Should().Be(stringValue);

			response.ApiCallDetails.ResponseBodyInBytes.Should().NotBeNull();
			memoryStreamFactory.Created.Count.Should().Be(3);
		}
	}

	[Fact]
	public async Task BytesResponse_WithoutDisableDirectStreaming_ShouldReturnExpectedResponse()
	{
		var nodePool = new SingleNodePool(Server.Uri);
		var memoryStreamFactory = new TrackingMemoryStreamFactory();
		var config = new TransportConfiguration(nodePool)
		{
			EnableHttpCompression = false,
			MemoryStreamFactory = memoryStreamFactory
		};
		var transport = new DistributedTransport(config);

		var payload = new Payload { ResponseString = EmptyJson, StatusCode = 200 };

		var response = await transport.PostAsync<BytesResponse>(Path, PostData.Serializable(payload));

		Validate(memoryStreamFactory, response);

		memoryStreamFactory.Reset();
		response = transport.Post<BytesResponse>(Path, PostData.Serializable(payload));

		Validate(memoryStreamFactory, response);

		static void Validate(TrackingMemoryStreamFactory memoryStreamFactory, BytesResponse response)
		{
			response.Body.AsSpan().SequenceEqual(EmptyJsonBytes);
			// Even when not using DisableDirectStreaming, we have a byte[] so the builder sets ResponseBodyInBytes too
			response.ApiCallDetails.ResponseBodyInBytes.Should().NotBeNull(); 
			memoryStreamFactory.Created.Count.Should().Be(2); // One required for setting request content and one to buffer the stream
			foreach (var memoryStream in memoryStreamFactory.Created)
				memoryStream.IsDisposed.Should().BeTrue();
		}
	}

	[Fact]
	public async Task BytesResponse_WithDisableDirectStreaming_ShouldReturnExpectedResponse()
	{
		var nodePool = new SingleNodePool(Server.Uri);
		var memoryStreamFactory = new TrackingMemoryStreamFactory();
		var config = new TransportConfiguration(nodePool)
		{
			EnableHttpCompression = false,
			DisableDirectStreaming = true,
			MemoryStreamFactory = memoryStreamFactory
		};
		var transport = new DistributedTransport(config);

		var payload = new Payload { ResponseString = EmptyJson, StatusCode = 200 };

		var response = await transport.PostAsync<BytesResponse>(Path, PostData.Serializable(payload));

		Validate(memoryStreamFactory, response);

		memoryStreamFactory.Reset();
		response = transport.Post<BytesResponse>(Path, PostData.Serializable(payload));

		Validate(memoryStreamFactory, response);

		static void Validate(TrackingMemoryStreamFactory memoryStreamFactory, BytesResponse response)
		{
			response.Body.AsSpan().SequenceEqual(EmptyJsonBytes);
			response.ApiCallDetails.ResponseBodyInBytes.Should().NotBeNull();
			memoryStreamFactory.Created.Count.Should().Be(3);
			foreach (var memoryStream in memoryStreamFactory.Created)
				memoryStream.IsDisposed.Should().BeTrue();
		}
	}

	[Fact]
	public async Task StreamResponse_WithoutDisableDirectStreaming_BodyShouldBeSet_NotDisposed_AndReadable()
	{
		var nodePool = new SingleNodePool(Server.Uri);
		var memoryStreamFactory = new TrackingMemoryStreamFactory();
		var config = new TransportConfiguration(nodePool)
		{
			EnableHttpCompression = false,
			MemoryStreamFactory = memoryStreamFactory
		};
		var transport = new DistributedTransport(config);

		var payload = new Payload { ResponseString = EmptyJson, StatusCode = 200 };

		var response = await transport.PostAsync<StreamResponse>(Path, PostData.Serializable(payload));

		await ValidateAsync(memoryStreamFactory, response);

		memoryStreamFactory.Reset();
		response = transport.Post<StreamResponse>(Path, PostData.Serializable(payload));

		await ValidateAsync(memoryStreamFactory, response);

		static async Task ValidateAsync(TrackingMemoryStreamFactory memoryStreamFactory, StreamResponse response)
		{
			response.Body.Should().NotBeSameAs(Stream.Null);
			response.ApiCallDetails.ResponseBodyInBytes.Should().BeNull();

			memoryStreamFactory.Created.Count.Should().Be(1);
			var sr = new StreamReader(response.Body);
			var result = await sr.ReadToEndAsync();
			result.Should().Be(EmptyJson);
		}
	}

	[Fact]
	public async Task StreamResponse_WithDisableDirectStreaming_BodyShouldBeSet_NotDisposed_AndReadable()
	{
		var nodePool = new SingleNodePool(Server.Uri);
		var memoryStreamFactory = new TrackingMemoryStreamFactory();
		var config = new TransportConfiguration(nodePool)
		{
			EnableHttpCompression = false,
			MemoryStreamFactory = memoryStreamFactory,
			DisableDirectStreaming = true
		};
		var transport = new DistributedTransport(config);

		var payload = new Payload { ResponseString = EmptyJson, StatusCode = 200 };
		var response = await transport.PostAsync<StreamResponse>(Path, PostData.Serializable(payload));

		await ValidateAsync(memoryStreamFactory, response);

		memoryStreamFactory.Reset();
		response = transport.Post<StreamResponse>(Path, PostData.Serializable(payload));

		await ValidateAsync(memoryStreamFactory, response);

		static async Task ValidateAsync(TrackingMemoryStreamFactory memoryStreamFactory, StreamResponse response)
		{
			response.Should().BeOfType<StreamResponse>();
			response.Body.Should().NotBeSameAs(Stream.Null);
			response.ApiCallDetails.ResponseBodyInBytes.Should().NotBeNull();

			// When disable direct streaming, we have 1 for the original content, 1 for the buffered request bytes and the last for the buffered response
			memoryStreamFactory.Created.Count.Should().Be(3);
			memoryStreamFactory.Created[0].IsDisposed.Should().BeTrue();
			memoryStreamFactory.Created[1].IsDisposed.Should().BeTrue();
			memoryStreamFactory.Created[2].IsDisposed.Should().BeFalse();

			var sr = new StreamReader(response.Body);
			var result = await sr.ReadToEndAsync();
			result.Should().Be(EmptyJson);
		}
	}

	[Fact]
	public async Task StringResponse_WithoutDisableDirectStreaming_MemoryStreamShouldBeDisposed()
	{
		var nodePool = new SingleNodePool(Server.Uri);
		var memoryStreamFactory = new TrackingMemoryStreamFactory();
		var config = new TransportConfiguration(nodePool)
		{
			EnableHttpCompression = false,
			MemoryStreamFactory = memoryStreamFactory
		};

		var transport = new DistributedTransport(config);

		var payload = new Payload { ResponseString = EmptyJson, StatusCode = 200 };
		var response = await transport.PostAsync<StringResponse>(Path, PostData.Serializable(payload));

		Validate(memoryStreamFactory, response);

		memoryStreamFactory.Reset();
		response = transport.Post<StringResponse>(Path, PostData.Serializable(payload));

		Validate(memoryStreamFactory, response);

		static void Validate(TrackingMemoryStreamFactory memoryStreamFactory, StringResponse response)
		{
			response.Should().BeOfType<StringResponse>();
			// All scenarios in the implementation buffer the bytes in some form and therefore expose those
			response.ApiCallDetails.ResponseBodyInBytes.Should().BeNull();

			// We expect one for the initial request stream
			memoryStreamFactory.Created.Count.Should().Be(1);
			foreach (var memoryStream in memoryStreamFactory.Created)
				memoryStream.IsDisposed.Should().BeTrue();

			response.Body.Should().Be(EmptyJson);
		}
	}

	[Fact]
	public async Task StringResponse_WithContentLongerThan1024_WithoutDisableDirectStreaming_BuildsExpectedResponse_AndMemoryStreamIsDisposed()
	{
		var nodePool = new SingleNodePool(Server.Uri);
		var memoryStreamFactory = new TrackingMemoryStreamFactory();
		var config = new TransportConfiguration(nodePool)
		{
			EnableHttpCompression = false,
			MemoryStreamFactory = memoryStreamFactory
		};

		var transport = new DistributedTransport(config);

		var payload = new Payload { ResponseString = LargeJson, StatusCode = 200 };
		var response = await transport.PostAsync<StringResponse>(Path, PostData.Serializable(payload));

		Validate(memoryStreamFactory, response);

		memoryStreamFactory.Reset();
		response = transport.Post<StringResponse>(Path, PostData.Serializable(payload));

		Validate(memoryStreamFactory, response);

		static void Validate(TrackingMemoryStreamFactory memoryStreamFactory, StringResponse response)
		{
			response.Should().BeOfType<StringResponse>();
			// All scenarios in the implementation buffer the bytes in some form and therefore expose those
			response.ApiCallDetails.ResponseBodyInBytes.Should().BeNull();

			// We expect one for the initial request stream
			memoryStreamFactory.Created.Count.Should().Be(1);
			foreach (var memoryStream in memoryStreamFactory.Created)
				memoryStream.IsDisposed.Should().BeTrue();

			response.Body.Should().Be(LargeJson);
		}
	}

	[Fact]
	public async Task WhenInvalidJson_WithDisableDirectStreaming_MemoryStreamShouldBeDisposed()
	{
		var nodePool = new SingleNodePool(Server.Uri);
		var memoryStreamFactory = new TrackingMemoryStreamFactory();
		var config = new TransportConfiguration(nodePool)
		{
			EnableHttpCompression = false,
			MemoryStreamFactory = memoryStreamFactory,
			DisableDirectStreaming = true
		};

		var transport = new DistributedTransport(config);

		var payload = new Payload { ResponseString = " " };
		var response = await transport.PostAsync<TestResponse>(Path, PostData.Serializable(payload));

		Validate(memoryStreamFactory, response);

		memoryStreamFactory.Reset();

		response = transport.Post<TestResponse>(Path, PostData.Serializable(payload));

		Validate(memoryStreamFactory, response);

		static void Validate(TrackingMemoryStreamFactory memoryStreamFactory, TestResponse response)
		{
			response.Should().BeOfType<TestResponse>();
			response.ApiCallDetails.ResponseBodyInBytes.Should().NotBeNull();

			memoryStreamFactory.Created.Count.Should().Be(3);
			foreach (var memoryStream in memoryStreamFactory.Created)
				memoryStream.IsDisposed.Should().BeTrue();

			response.Value.Should().Be(string.Empty);
		}
	}

	[Fact]
	public async Task WhenInvalidJson_WithoutDisableDirectStreaming_MemoryStreamShouldBeDisposed()
	{
		var nodePool = new SingleNodePool(Server.Uri);
		var memoryStreamFactory = new TrackingMemoryStreamFactory();
		var config = new TransportConfiguration(nodePool)
		{
			EnableHttpCompression = false,
			MemoryStreamFactory = memoryStreamFactory
		};

		var transport = new DistributedTransport(config);

		var payload = new Payload { ResponseString = " " };
		var response = await transport.PostAsync<TestResponse>(Path, PostData.Serializable(payload));

		Validate(memoryStreamFactory, response);

		memoryStreamFactory.Reset();

		response = transport.Post<TestResponse>(Path, PostData.Serializable(payload));

		Validate(memoryStreamFactory, response);

		static void Validate(TrackingMemoryStreamFactory memoryStreamFactory, TestResponse response)
		{
			response.Should().BeOfType<TestResponse>();
			response.ApiCallDetails.ResponseBodyInBytes.Should().BeNull();

			memoryStreamFactory.Created.Count.Should().Be(1);
			foreach (var memoryStream in memoryStreamFactory.Created)
				memoryStream.IsDisposed.Should().BeTrue();

			response.Value.Should().Be(string.Empty);
		}
	}

	[Fact]
	public async Task WhenNoContent_MemoryStreamShouldBeDisposed()
	{
		var nodePool = new SingleNodePool(Server.Uri);
		var memoryStreamFactory = new TrackingMemoryStreamFactory();
		var config = new TransportConfiguration(nodePool)
		{
			EnableHttpCompression = false,
			MemoryStreamFactory = memoryStreamFactory,
		};

		var transport = new DistributedTransport(config);

		var payload = new Payload { ResponseString = "", StatusCode = 204 };
		var response = await transport.PostAsync<TestResponse>(Path, PostData.Serializable(payload));

		Validate(memoryStreamFactory, response);

		memoryStreamFactory.Reset();

		response = transport.Post<TestResponse>(Path, PostData.Serializable(payload));

		Validate(memoryStreamFactory, response);

		static void Validate(TrackingMemoryStreamFactory memoryStreamFactory, TestResponse response)
		{
			response.Should().BeOfType<TestResponse>();
			response.ApiCallDetails.ResponseBodyInBytes.Should().BeNull();

			memoryStreamFactory.Created.Count.Should().Be(1);
			foreach (var memoryStream in memoryStreamFactory.Created)
				memoryStream.IsDisposed.Should().BeTrue();

			response.Value.Should().Be(string.Empty);
		}
	}

	[Fact]
	public async Task WhenNoContent_WithDisableDirectStreaming_MemoryStreamShouldBeDisposed()
	{
		var nodePool = new SingleNodePool(Server.Uri);
		var memoryStreamFactory = new TrackingMemoryStreamFactory();
		var config = new TransportConfiguration(nodePool)
		{
			EnableHttpCompression = false,
			MemoryStreamFactory = memoryStreamFactory,
			DisableDirectStreaming = true
		};

		var transport = new DistributedTransport(config);

		var payload = new Payload { ResponseString = "", StatusCode = 204 };
		var response = await transport.PostAsync<TestResponse>(Path, PostData.Serializable(payload));

		Validate(memoryStreamFactory, response);

		memoryStreamFactory.Reset();

		response = transport.Post<TestResponse>(Path, PostData.Serializable(payload));

		Validate(memoryStreamFactory, response);

		static void Validate(TrackingMemoryStreamFactory memoryStreamFactory, TestResponse response)
		{
			response.Should().BeOfType<TestResponse>();
			response.ApiCallDetails.ResponseBodyInBytes.Should().BeNull();

			// We expect one for sending the request payload, but as the response is 204, we shouldn't
			// see other memory streams being created for the response.
			memoryStreamFactory.Created.Count.Should().Be(2);
			foreach (var memoryStream in memoryStreamFactory.Created)
				memoryStream.IsDisposed.Should().BeTrue();

			response.Value.Should().Be(string.Empty);
		}
	}

	[Fact]
	public async Task PlainText_WithoutCustomResponseBuilder_WithDisableDirectStreaming_MemoryStreamShouldBeDisposed()
	{
		const string expectedString = "test-value";

		var nodePool = new SingleNodePool(Server.Uri);
		var memoryStreamFactory = new TrackingMemoryStreamFactory();
		var config = new TransportConfiguration(nodePool)
		{
			EnableHttpCompression = false,
			MemoryStreamFactory = memoryStreamFactory,
			DisableDirectStreaming = true,
			ContentType = "application/json"
		};

		var transport = new DistributedTransport(config);

		var payload = new Payload { ResponseString = expectedString, ContentType = "text/plain" };
		var response = await transport.PostAsync<TestResponse>(Path, PostData.Serializable(payload));

		Validate(memoryStreamFactory, response);

		memoryStreamFactory.Reset();

		response = transport.Post<TestResponse>(Path, PostData.Serializable(payload));

		Validate(memoryStreamFactory, response);

		static void Validate(TrackingMemoryStreamFactory memoryStreamFactory, TestResponse response)
		{
			memoryStreamFactory.Created.Count.Should().Be(3);
			foreach (var memoryStream in memoryStreamFactory.Created)
				memoryStream.IsDisposed.Should().BeTrue();

			response.ApiCallDetails.ResponseBodyInBytes.Should().NotBeNull();
			response.Value.Should().Be(string.Empty); // default value as no custom builder

			var value = Encoding.UTF8.GetString(response.ApiCallDetails.ResponseBodyInBytes);
			value.Should().Be(expectedString); // The buffered bytes should include the response string
		}
	}

	[Fact]
	public async Task PlainText_WithoutCustomResponseBuilder_WithoutDisableDirectStreaming_MemoryStreamShouldBeDisposed()
	{
		const string expectedString = "test-value";

		var nodePool = new SingleNodePool(Server.Uri);
		var memoryStreamFactory = new TrackingMemoryStreamFactory();
		var config = new TransportConfiguration(nodePool)
		{
			EnableHttpCompression = false,
			MemoryStreamFactory = memoryStreamFactory,
			ContentType = "application/json"
		};

		var transport = new DistributedTransport(config);

		var payload = new Payload { ResponseString = expectedString, ContentType = "text/plain" };
		var response = await transport.PostAsync<TestResponse>(Path, PostData.Serializable(payload));

		Validate(memoryStreamFactory, response);

		memoryStreamFactory.Reset();

		response = transport.Post<TestResponse>(Path, PostData.Serializable(payload));

		Validate(memoryStreamFactory, response);

		static void Validate(TrackingMemoryStreamFactory memoryStreamFactory, TestResponse response)
		{
			memoryStreamFactory.Created.Count.Should().Be(1);
			foreach (var memoryStream in memoryStreamFactory.Created)
				memoryStream.IsDisposed.Should().BeTrue();

			response.ApiCallDetails.ResponseBodyInBytes.Should().BeNull();
			response.Value.Should().Be(string.Empty); // default value as no custom builder
		}
	}

	[Fact]
	public async Task PlainText_WithoutCustomResponseBuilder_WithoutDisableDirectStreaming__AcceptingPlainText_MemoryStreamShouldBeDisposed()
	{
		const string expectedString = "test-value";

		var nodePool = new SingleNodePool(Server.Uri);
		var memoryStreamFactory = new TrackingMemoryStreamFactory();
		var config = new TransportConfiguration(nodePool)
		{
			EnableHttpCompression = false,
			MemoryStreamFactory = memoryStreamFactory,
			ContentType = "application/json"
		};

		var transport = new DistributedTransport(config);

		var requestConfig = new RequestConfiguration { Accept = "text/plain" };
		var payload = new Payload { ResponseString = expectedString, ContentType = "text/plain" };

		await transport.Invoking(async t => await t.RequestAsync<TestResponse>(new EndpointPath(HttpMethod.POST, Path), PostData.Serializable(payload), default, requestConfig))
			.Should()
				.ThrowAsync<UnexpectedTransportException>("when there is no custom builder, it falls through to the default builder using STJ.")
				.WithInnerException<UnexpectedTransportException, JsonException>();

		transport.Invoking(t => t.Request<TestResponse>(new EndpointPath(HttpMethod.POST, Path), PostData.Serializable(payload), default, requestConfig))
			.Should()
				.Throw<UnexpectedTransportException>("when there is no custom builder, it falls through to the default builder using STJ.")
				.WithInnerException<JsonException>();
	}

	[Fact]
	public async Task PlainText_WithoutCustomResponseBuilder_WithoutDisableDirectStreaming_WhenChunkedResponse_MemoryStreamShouldBeDisposed()
	{
		const string expectedString = "test-value";

		var nodePool = new SingleNodePool(Server.Uri);
		var memoryStreamFactory = new TrackingMemoryStreamFactory();
		var config = new TransportConfiguration(nodePool)
		{
			EnableHttpCompression = false,
			MemoryStreamFactory = memoryStreamFactory,
			ContentType = "application/json"
		};

		var transport = new DistributedTransport(config);

		var payload = new Payload { ResponseString = expectedString, ContentType = "text/plain", IsChunked = true };
		var response = await transport.PostAsync<TestResponse>(Path, PostData.Serializable(payload));

		Validate(memoryStreamFactory, response);

		memoryStreamFactory.Reset();

		response = transport.Post<TestResponse>(Path, PostData.Serializable(payload));

		Validate(memoryStreamFactory, response);

		static void Validate(TrackingMemoryStreamFactory memoryStreamFactory, TestResponse response)
		{
			memoryStreamFactory.Created.Count.Should().Be(1);
			foreach (var memoryStream in memoryStreamFactory.Created)
				memoryStream.IsDisposed.Should().BeTrue();

			response.ApiCallDetails.ResponseBodyInBytes.Should().BeNull();
			response.Value.Should().Be(string.Empty); // default value as no custom builder
		}
	}

	[Fact]
	public async Task PlainText_WithCustomResponseBuilder_WithDisableDirectStreaming_MemoryStreamShouldBeDisposed()
	{
		const string expectedString = "test-value";

		var nodePool = new SingleNodePool(Server.Uri);
		var memoryStreamFactory = new TrackingMemoryStreamFactory();
		var config = new TransportConfiguration(nodePool)
		{
			EnableHttpCompression = false,
			MemoryStreamFactory = memoryStreamFactory,
			DisableDirectStreaming = true,
			ContentType = "application/json",
			ResponseBuilders = [new TestResponseBuilder()]
		};

		var transport = new DistributedTransport(config);

		var requestConfig = new RequestConfiguration { Accept = "text/plain" };
		var payload = new Payload { ResponseString = expectedString, ContentType = "text/plain" };
		var response = await transport.RequestAsync<TestResponse>(new EndpointPath(HttpMethod.POST, Path), PostData.Serializable(payload), default, requestConfig);

		memoryStreamFactory.Created.Count.Should().Be(3);
		foreach (var memoryStream in memoryStreamFactory.Created)
			memoryStream.IsDisposed.Should().BeTrue();

		response.ApiCallDetails.ResponseBodyInBytes.Should().NotBeNull();
		response.Value.Should().Be(expectedString);

		var value = Encoding.UTF8.GetString(response.ApiCallDetails.ResponseBodyInBytes);
		value.Should().Be(expectedString);

		memoryStreamFactory.Reset();

		response = transport.Request<TestResponse>(new EndpointPath(HttpMethod.POST, Path), PostData.Serializable(payload), default, requestConfig);

		memoryStreamFactory.Created.Count.Should().Be(3);
		foreach (var memoryStream in memoryStreamFactory.Created)
			memoryStream.IsDisposed.Should().BeTrue();

		response.ApiCallDetails.ResponseBodyInBytes.Should().NotBeNull();
		response.Value.Should().Be(expectedString);
	}

	[Fact]
	public async Task PlainText_WithCustomResponseBuilder_WithoutDisableDirectStreaming()
	{
		const string expectedString = "test-value";

		var nodePool = new SingleNodePool(Server.Uri);
		var memoryStreamFactory = new TrackingMemoryStreamFactory();
		var config = new TransportConfiguration(nodePool)
		{
			EnableHttpCompression = false,
			MemoryStreamFactory = memoryStreamFactory,
			DisableDirectStreaming = false,
			ContentType = "application/json",
			ResponseBuilders = [new TestResponseBuilder()]
		};

		var transport = new DistributedTransport(config);

		var requestConfig = new RequestConfiguration { Accept = "text/plain" };
		var payload = new Payload { ResponseString = expectedString, ContentType = "text/plain" };
		var response = await transport.RequestAsync<TestResponse>(new EndpointPath(HttpMethod.POST, Path), PostData.Serializable(payload), default, requestConfig);

		memoryStreamFactory.Created.Count.Should().Be(1);
		response.ApiCallDetails.ResponseBodyInBytes.Should().BeNull();
		response.Value.Should().Be(expectedString);

		memoryStreamFactory.Reset();

		response = transport.Request<TestResponse>(new EndpointPath(HttpMethod.POST, Path), PostData.Serializable(payload), default, requestConfig);
				
		memoryStreamFactory.Created.Count.Should().Be(1);
		response.ApiCallDetails.ResponseBodyInBytes.Should().BeNull();
		response.Value.Should().Be(expectedString);
	}

	private class TestResponse : TransportResponse
	{
		public string Value { get; internal set; } = string.Empty;
	};

	private class TestResponseBuilder : TypedResponseBuilder<TestResponse>
	{
		protected override TestResponse Build(ApiCallDetails apiCallDetails, BoundConfiguration boundConfiguration, Stream responseStream, string contentType, long contentLength)
		{
			var sr = new StreamReader(responseStream);
			var value = sr.ReadToEnd();
			return new TestResponse { Value = value };
		}

		protected override async Task<TestResponse> BuildAsync(ApiCallDetails apiCallDetails, BoundConfiguration boundConfiguration, Stream responseStream,
			string contentType, long contentLength, CancellationToken cancellationToken = default)
		{
			var sr = new StreamReader(responseStream);
			var value = await sr.ReadToEndAsync(cancellationToken);
			return new TestResponse { Value = value };
		}
	}
}

public class Payload
{
	public string ResponseString { get; set; } = "{}";
	public string ContentType { get; set; } = "application/json";
	public int StatusCode { get; set; } = 200;
	public bool IsChunked { get; set; } = false;
}

[ApiController, Route("[controller]")]
public class SpecialResponseController : ControllerBase
{
	[HttpPost]
	public async Task Post([FromBody] Payload payload)
	{
		var bytes = Encoding.UTF8.GetBytes(payload.ResponseString);

		Response.ContentType = payload.ContentType;
		Response.StatusCode = payload.StatusCode;

		if (!payload.IsChunked)
		{
			Response.ContentLength = bytes.Length;
		}

		if (payload.StatusCode != 204)
		{
			await Response.BodyWriter.WriteAsync(bytes);
			await Response.BodyWriter.CompleteAsync();
		}
	}
}
