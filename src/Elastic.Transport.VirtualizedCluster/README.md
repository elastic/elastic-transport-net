# Elastic.Transport.VirtualizedCluster

An in-memory virtual cluster for testing [Elastic.Transport](https://www.nuget.org/packages/Elastic.Transport) behavior without connecting to a real cluster. Provides a fluent rule engine to simulate ping, sniff, and API call responses, and an auditor to assert the exact sequence of transport actions.

## Installation

```
dotnet add package Elastic.Transport.VirtualizedCluster
```

## Quick Start

```csharp
// Bootstrap a 3-node virtual cluster
var cluster = Virtual.Elasticsearch
    .Bootstrap(3)
    .ClientCalls(r => r.SucceedAlways())
    .StaticNodePool()
    .AllDefaults();

var response = cluster.ClientCall();
// response.ApiCallDetails.HttpStatusCode == 200
```

## Rule Engine

Rules control how the virtual cluster responds to pings, sniffs, and client API calls.

### Client Call Rules

```csharp
Virtual.Elasticsearch
    .Bootstrap(3)
    .ClientCalls(r => r.SucceedAlways())
    // or fail a certain number of times, then succeed
    .ClientCalls(r => r.Fails(TimesHelper.Times(2)).Succeeds(TimesHelper.Times(10)))
    // simulate slow responses
    .ClientCalls(r => r.Takes(TimeSpan.FromSeconds(10)).SucceedAlways())
    // return a custom response body
    .ClientCalls(r => r.ReturnResponse(new { hits = new { total = 100 } }).SucceedAlways())
    .StaticNodePool()
    .AllDefaults();
```

### Ping Rules

```csharp
Virtual.Elasticsearch
    .Bootstrap(3)
    .Ping(r => r.SucceedAlways())
    // or fail on a specific port
    .Ping(r => r.OnPort(9201).Fails(TimesHelper.Times(1)))
    .Ping(r => r.OnPort(9202).SucceedAlways())
    .StaticNodePool()
    .AllDefaults();
```

### Sniff Rules

```csharp
Virtual.Elasticsearch
    .Bootstrap(3)
    .Sniff(r => r.SucceedAlways())
    // or return a new cluster topology after sniffing
    .Sniff(r => r.Succeeds(TimesHelper.Always, Virtual.Elasticsearch.Bootstrap(5)))
    .SniffingNodePool()
    .AllDefaults();
```

## Node Pool Strategies

Choose the node pool to test different failover and discovery behaviors:

```csharp
var cluster = Virtual.Elasticsearch.Bootstrap(10);

cluster.StaticNodePool()           // Fixed list of nodes
cluster.SniffingNodePool()         // Discovers nodes via sniff API
cluster.StickyNodePool()           // Prefers a single node
cluster.StickySniffingNodePool()   // Sticky with sniff discovery
cluster.SingleNodeConnection()     // Single node, no failover
```

## Auditing

The `Auditor` class asserts the exact sequence of transport events across both sync and async code paths:

```csharp
var auditor = new Auditor(() => Virtual.Elasticsearch
    .Bootstrap(3)
    .Ping(r => r.SucceedAlways())
    .ClientCalls(r => r.SucceedAlways())
    .StaticNodePool()
    .AllDefaults()
);

await auditor.TraceCall(new ClientCall
{
    { AuditEvent.PingSuccess, 9200 },
    { AuditEvent.HealthyResponse, 9200 }
});
```

## Time Control

Simulate the passage of time to test dead-node resurrection and timeout behavior:

```csharp
var cluster = Virtual.Elasticsearch
    .Bootstrap(3)
    .ClientCalls(r => r.FailAlways())
    .StaticNodePool()
    .AllDefaults();

// Advance time by 30 minutes
cluster.ChangeTime(d => d.AddMinutes(30));

// Nodes marked dead will now be retried
var response = cluster.ClientCall();
```

## Custom Configuration

Use `.Settings()` to customize the transport configuration:

```csharp
var cluster = Virtual.Elasticsearch
    .Bootstrap(3)
    .ClientCalls(r => r.SucceedAlways())
    .StaticNodePool()
    .Settings(s => s with
    {
        DisablePings = true,
        MaxRetries = 5
    });
```

## Links

- [GitHub Repository](https://github.com/elastic/elastic-transport-net)
- [Elastic.Transport on NuGet](https://www.nuget.org/packages/Elastic.Transport)
