using System;
using System.Text.Json.Serialization;
using Elastic.Transport;
using Elastic.Transport.Experimental;
using Elastic.Transport.Products.Elasticsearch;
using Playground;

var transport = new Transport(new TransportConfiguration(new SingleNodeConnectionPool(new Uri("http://localhost:9600")),
	new LowLevelConnection(), new LowLevelRequestResponseSerializer(), new ElasticsearchProductRegistration()));

for (var i = 0; i < 5; i++)
{
	var response = await transport.RequestAsync<ClusterHealthResponse>(HttpMethod.GET, "_cluster/health", default);
	Console.WriteLine(response.ClusterName);
}

Console.WriteLine("Press a key to exit.");
Console.ReadKey();

namespace Playground
{
	public class ClusterHealthResponse : ITransportResponse
	{
		[JsonIgnore]
		public IApiCallDetails ApiCall { get; set; }

		[JsonInclude, JsonPropertyName("cluster_name")]
		public string ClusterName { get; internal set; }

		[JsonInclude, JsonPropertyName("active_primary_shards")]
		public int ActivePrimaryShards { get; internal set; }

		[JsonInclude, JsonPropertyName("active_shards")]
		public int ActiveShards { get; internal set; }

		[JsonInclude, JsonPropertyName("active_shards_percent_as_number")]
		public double ActiveShardsPercentAsNumber { get; internal set; }

		[JsonInclude, JsonPropertyName("delayed_unassigned_shards")]
		public int DelayedUnassignedShards { get; internal set; }

		[JsonInclude, JsonPropertyName("initializing_shards")]
		public int InitializingShards { get; internal set; }

		[JsonInclude, JsonPropertyName("number_of_data_nodes")]
		public int NumberOfDataNodes { get; internal set; }

		[JsonInclude, JsonPropertyName("number_of_in_flight_fetch")]
		public int NumberOfInFlightFetch { get; internal set; }
		
		[JsonInclude, JsonPropertyName("number_of_nodes")]
		public int NumberOfNodes { get; internal set; }

		[JsonInclude, JsonPropertyName("number_of_pending_tasks")]
		public int NumberOfPendingTasks { get; internal set; }

		[JsonInclude, JsonPropertyName("relocating_shards")]
		public int RelocatingShards { get; internal set; }

		[JsonInclude, JsonPropertyName("status")]
		public Health Status { get; internal set; }

		[JsonInclude, JsonPropertyName("task_max_waiting_in_queue_millis")]
		public long TaskMaxWaitingInQueueMillis { get; internal set; }

		[JsonInclude, JsonPropertyName("timed_out")]
		public bool TimedOut { get; internal set; }

		[JsonInclude, JsonPropertyName("unassigned_shards")]
		public int UnassignedShards { get; internal set; }
	}

	public enum Health
	{
		Green,
		Yellow,
		Red
	}
}
