// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.IO;
using System.Text;
using System.Threading.Tasks;
using Elastic.Transport.Tests.Shared;
using FluentAssertions;
using Xunit;

namespace Elastic.Transport.Tests.Responses.Special;

public class StringResponseBuilderTests
{
	private const string Json = "{\"_index\":\"my-index\",\"_id\":\"pZqC6JIB9RdSpcF8-3lq\",\"_version\":1,\"result\"" +
		":\"created\",\"_shards\":{\"total\":1,\"successful\":1,\"failed\":0},\"_seq_no\":2,\"_primary_term\":1}";

	private static readonly byte[] Data = Encoding.UTF8.GetBytes(Json);

	[Fact]
	public async Task ReturnsExpectedResponse()
	{
		IResponseBuilder sut = new StringResponseBuilder();

		var memoryStreamFactory = new TrackingMemoryStreamFactory();
		var config = new TransportConfiguration() { MemoryStreamFactory = memoryStreamFactory };
		var apiCallDetails = new ApiCallDetails();
		var boundConfiguration = new BoundConfiguration(config);
		var stream = new MemoryStream(Data);

		var result = await sut.BuildAsync<StringResponse>(apiCallDetails, boundConfiguration, stream, BoundConfiguration.DefaultContentType, Data.Length);
		result.Body.Should().Be(Json);

		memoryStreamFactory.Created.Count.Should().Be(0);

		stream.Position = 0;
		memoryStreamFactory.Reset();

		result = sut.Build<StringResponse>(apiCallDetails, boundConfiguration, stream, BoundConfiguration.DefaultContentType, Data.Length);
		result.Body.Should().Be(Json);

		memoryStreamFactory.Created.Count.Should().Be(0);
	}

	[Fact]
	public async Task ReturnsExpectedResponse_WhenLargeNonChunkedResponse()
	{
		const string largeJson = "[{\"_id\":\"672b13c7666cae7721b7f5c8\",\"index\":0,\"guid\":\"f8a9356c-660b-4f4f-a1c2-84048e0599b9\",\"isActive\":false,\"balance\":\"$3,856.23\"," +
			"\"picture\":\"http://placehold.it/32x32\",\"age\":29,\"eyeColor\":\"green\",\"name\":\"Noemi Reed\",\"gender\":\"female\",\"company\":\"LUNCHPOD\",\"email\":" +
			"\"noemireed@lunchpod.com\",\"phone\":\"+1 (961) 417-3668\",\"address\":\"954 Cameron Court, Onton, South Dakota, 1148\",\"about\":\"Qui ad id veniam aute amet " +
			"commodo officia est cillum. Elit nostrud Lorem tempor duis. Commodo velit nulla nisi velit laborum qui minim nostrud aute dolor tempor officia. Commodo proident " +
			"nulla eu adipisicing incididunt eu. Quis nostrud Lorem amet deserunt pariatur ea elit adipisicing qui. Voluptate exercitation id esse tempor occaecat.\\r\\n\"," +
			"\"registered\":\"2017-02-28T04:33:12 -00:00\",\"latitude\":30.32678,\"longitude\":-156.977981,\"tags\":[\"sit\",\"culpa\",\"cillum\",\"labore\",\"in\",\"labore\"," +
			"\"quis\"],\"friends\":[{\"id\":0,\"name\":\"Good Lyons\"},{\"id\":1,\"name\":\"Mccarthy Delaney\"},{\"id\":2,\"name\":\"Winters Combs\"}],\"greeting\":\"Hello, " +
			"Noemi Reed! You have 8 unread messages.\",\"favoriteFruit\":\"strawberry\"},{\"_id\":\"672b13c741693abd9d0173a9\",\"index\":1,\"guid\":" +
			"\"fa3d27ec-213c-4365-92e9-39774eec9d01\",\"isActive\":false,\"balance\":\"$2,275.63\",\"picture\":\"http://placehold.it/32x32\",\"age\":23,\"eyeColor\":\"brown\"," +
			"\"name\":\"Cooley Williams\",\"gender\":\"male\",\"company\":\"GALLAXIA\",\"email\":\"cooleywilliams@gallaxia.com\",\"phone\":\"+1 (961) 439-2700\",\"address\":" +
			"\"791 Montgomery Place, Garfield, Guam, 9900\",\"about\":\"Officia consectetur do quis id cillum quis esse. Aliqua deserunt eiusmod laboris cupidatat enim commodo " +
			"est Lorem id nisi mollit non. Eiusmod adipisicing pariatur culpa nostrud incididunt dolor commodo fugiat amet ex dolor ex. Nostrud incididunt consequat ullamco " +
			"pariatur cupidatat nulla eu voluptate cupidatat nulla. Mollit est id adipisicing ad mollit exercitation. Ullamco non ad aliquip ea sit culpa pariatur commodo " +
			"veniam. In occaecat et tempor ea Lorem eu incididunt sit commodo officia.\\r\\n\",\"registered\":\"2019-05-25T11:41:44 -01:00\",\"latitude\":-85.996713,\"longitude\"" +
			":-140.910029,\"tags\":[\"esse\",\"qui\",\"magna\",\"et\",\"irure\",\"est\",\"in\"],\"friends\":[{\"id\":0,\"name\":\"Pamela Castillo\"},{\"id\":1,\"name\"" +
			":\"Suzanne Herman\"},{\"id\":2,\"name\":\"Gonzales Bush\"}],\"greeting\":\"Hello, Cooley Williams! You have 8 unread messages.\",\"favoriteFruit\":\"apple\"}]";

		var data = Encoding.UTF8.GetBytes(largeJson);

		IResponseBuilder sut = new StringResponseBuilder();

		var memoryStreamFactory = new TrackingMemoryStreamFactory();
		var config = new TransportConfiguration() { MemoryStreamFactory = memoryStreamFactory };
		var apiCallDetails = new ApiCallDetails();
		var boundConfiguration = new BoundConfiguration(config);
		var stream = new MemoryStream(data);

		var result = await sut.BuildAsync<StringResponse>(apiCallDetails, boundConfiguration, stream, BoundConfiguration.DefaultContentType, data.Length);
		result.Body.Should().Be(largeJson);

		memoryStreamFactory.Created.Count.Should().Be(0);

		stream.Position = 0;
		memoryStreamFactory.Reset();

		result = sut.Build<StringResponse>(apiCallDetails, boundConfiguration, stream, BoundConfiguration.DefaultContentType, data.Length);
		result.Body.Should().Be(largeJson);

		memoryStreamFactory.Created.Count.Should().Be(0);
	}

	[Fact]
	public async Task ReturnsExpectedResponse_WhenLargeChunkedResponse()
	{
		const string largeJson = "[{\"_id\":\"672b13c7666cae7721b7f5c8\",\"index\":0,\"guid\":\"f8a9356c-660b-4f4f-a1c2-84048e0599b9\",\"isActive\":false,\"balance\":\"$3,856.23\"," +
			"\"picture\":\"http://placehold.it/32x32\",\"age\":29,\"eyeColor\":\"green\",\"name\":\"Noemi Reed\",\"gender\":\"female\",\"company\":\"LUNCHPOD\",\"email\":" +
			"\"noemireed@lunchpod.com\",\"phone\":\"+1 (961) 417-3668\",\"address\":\"954 Cameron Court, Onton, South Dakota, 1148\",\"about\":\"Qui ad id veniam aute amet " +
			"commodo officia est cillum. Elit nostrud Lorem tempor duis. Commodo velit nulla nisi velit laborum qui minim nostrud aute dolor tempor officia. Commodo proident " +
			"nulla eu adipisicing incididunt eu. Quis nostrud Lorem amet deserunt pariatur ea elit adipisicing qui. Voluptate exercitation id esse tempor occaecat.\\r\\n\"," +
			"\"registered\":\"2017-02-28T04:33:12 -00:00\",\"latitude\":30.32678,\"longitude\":-156.977981,\"tags\":[\"sit\",\"culpa\",\"cillum\",\"labore\",\"in\",\"labore\"," +
			"\"quis\"],\"friends\":[{\"id\":0,\"name\":\"Good Lyons\"},{\"id\":1,\"name\":\"Mccarthy Delaney\"},{\"id\":2,\"name\":\"Winters Combs\"}],\"greeting\":\"Hello, " +
			"Noemi Reed! You have 8 unread messages.\",\"favoriteFruit\":\"strawberry\"},{\"_id\":\"672b13c741693abd9d0173a9\",\"index\":1,\"guid\":" +
			"\"fa3d27ec-213c-4365-92e9-39774eec9d01\",\"isActive\":false,\"balance\":\"$2,275.63\",\"picture\":\"http://placehold.it/32x32\",\"age\":23,\"eyeColor\":\"brown\"," +
			"\"name\":\"Cooley Williams\",\"gender\":\"male\",\"company\":\"GALLAXIA\",\"email\":\"cooleywilliams@gallaxia.com\",\"phone\":\"+1 (961) 439-2700\",\"address\":" +
			"\"791 Montgomery Place, Garfield, Guam, 9900\",\"about\":\"Officia consectetur do quis id cillum quis esse. Aliqua deserunt eiusmod laboris cupidatat enim commodo " +
			"est Lorem id nisi mollit non. Eiusmod adipisicing pariatur culpa nostrud incididunt dolor commodo fugiat amet ex dolor ex. Nostrud incididunt consequat ullamco " +
			"pariatur cupidatat nulla eu voluptate cupidatat nulla. Mollit est id adipisicing ad mollit exercitation. Ullamco non ad aliquip ea sit culpa pariatur commodo " +
			"veniam. In occaecat et tempor ea Lorem eu incididunt sit commodo officia.\\r\\n\",\"registered\":\"2019-05-25T11:41:44 -01:00\",\"latitude\":-85.996713,\"longitude\"" +
			":-140.910029,\"tags\":[\"esse\",\"qui\",\"magna\",\"et\",\"irure\",\"est\",\"in\"],\"friends\":[{\"id\":0,\"name\":\"Pamela Castillo\"},{\"id\":1,\"name\"" +
			":\"Suzanne Herman\"},{\"id\":2,\"name\":\"Gonzales Bush\"}],\"greeting\":\"Hello, Cooley Williams! You have 8 unread messages.\",\"favoriteFruit\":\"apple\"}]";

		var data = Encoding.UTF8.GetBytes(largeJson);

		IResponseBuilder sut = new StringResponseBuilder();

		var memoryStreamFactory = new TrackingMemoryStreamFactory();
		var config = new TransportConfiguration() { MemoryStreamFactory = memoryStreamFactory };
		var apiCallDetails = new ApiCallDetails();
		var boundConfiguration = new BoundConfiguration(config);
		var stream = new MemoryStream(data);

		var result = await sut.BuildAsync<StringResponse>(apiCallDetails, boundConfiguration, stream, BoundConfiguration.DefaultContentType, -1);
		result.Body.Should().Be(largeJson);

		memoryStreamFactory.Created.Count.Should().Be(0);

		stream.Position = 0;
		memoryStreamFactory.Reset();

		result = sut.Build<StringResponse>(apiCallDetails, boundConfiguration, stream, BoundConfiguration.DefaultContentType, -1);
		result.Body.Should().Be(largeJson);

		memoryStreamFactory.Created.Count.Should().Be(0);
	}

	[Fact]
	public async Task ReturnsExpectedResponse_WhenDisableDirectStreaming()
	{
		IResponseBuilder sut = new StringResponseBuilder();

		var memoryStreamFactory = new TrackingMemoryStreamFactory();
		var config = new TransportConfiguration() { MemoryStreamFactory = memoryStreamFactory, DisableDirectStreaming = true };
		var apiCallDetails = new ApiCallDetails() { ResponseBodyInBytes = Data };
		var boundConfiguration = new BoundConfiguration(config);
		var stream = new MemoryStream(Data);

		var result = await sut.BuildAsync<StringResponse>(apiCallDetails, boundConfiguration, stream, BoundConfiguration.DefaultContentType, -1);
		result.Body.Should().Be(Json);

		memoryStreamFactory.Created.Count.Should().Be(0);

		stream.Position = 0;
		memoryStreamFactory.Reset();

		result = sut.Build<StringResponse>(apiCallDetails, boundConfiguration, stream, BoundConfiguration.DefaultContentType, -1);
		result.Body.Should().Be(Json);

		memoryStreamFactory.Created.Count.Should().Be(0);
	}
}
