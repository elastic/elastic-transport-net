# `Elastic.Transport`

Transport classes and utilities shared among .NET Elastic client libraries

This library was lifted from [elasticsearch-net](https://github.com/elastic/elasticsearch-net) and then transformed to be used across all Elastic services rather than only Elasticsearch.


Provides the clients connectivity components, exposes a (potentially) cluster aware request pipeline that can be resilient to nodes dropping in & out of rotation.  
This package is heavily optimized for the Elastic (elastic.co) product suite and Elastic Cloud (cloud.elastic.co) SAAS offering. 

The transport is designed to fail over fast and in constant time. 
If a `Node` is considered bad it will fail-over only immediately given the overall request timeout allows for it.

It's an explicit non-goal to introduce full (incremental) retry mechanisms. This library is too generic to be making
these decisions and should be pushed on to the products/libraries making use of `Elastic.Transport`


## Usage

This library can be used on its own but is typically used as the heart of a facade client that models all 
the API endpoints.

In its most direct and terse form you  can use the following to create requests.

```c#
var settings = new TransportConfiguration(new Uri("http://localhost:9200"));
var transport = new Transport(settings);

var response = transport.Get<StringResponse>("/");
var headResponse = transport.Head("/");
```

`Get` and `Head` are extension methods to  the only method `ITransport` dictates namely `Request()` and its async variant.

Wrapping clients most likely will list out all `components` explicitly and use `Transport<TConfiguration>`


```c#
var pool = new StaticNodePool(new[] {new Node(new Uri("http://localhost:9200"))});
var connection = new HttpConnection();
var serializer = LowLevelRequestResponseSerializer.Instance;
var product = ElasticsearchProductRegistration.Default;

var settings = new TransportConfiguration(pool, connection, serializer, product);
var transport = new Transport<TransportConfiguration>(settings);

var response = transport.Request<StringResponse>(HttpMethod.GET, "/");
```

This allows implementers to extend `TransportConfiguration` with product/service specific configuration.


### Components

`ITransport` itself only defines `Request()` and `RequestAsync()` and all wrapping clients accept an `ITransport`.

The `ITransport` implementation that this library ships models a request pipeline that can deal with a large variety of topologies

![request-pipeline.png](request-pipeline.png)

Whilst complex every effort is made to only walk paths if we are certain they provide value.

It introduces two special API calls 

* `sniff` a request to the service/product that should inform the client about the active topology.
* `ping` the fastest request the transport can do to validate a `Node` is alive.


If you instantiate `Transport` you can pass an instance of `IProductRegistation` to provide implementations
for `sniff` and `ping`.

```c#
var settings = new TransportConfiguration(new Uri("http://localhost:9200"));
var transport = new Transport(settings);
```

Will use the `DefaultProductRegistration` wich opts out of `sniff` and `ping`

However this library ship with a default implementation to fill in the blanks for `Elasticsearch`
so we can create a transport for Elasticsearch that support `sniff` and `ping` as followed

```c#
var uri = new Uri("http://localhost:9200");
var settings = new TransportConfiguration(uri, ElasticsearchProductRegistration.Default);
var transport = new Transport(settings);
```

### Injection

All components are optional and ship with sane defaults. Typically client users only provide
the `NodePool` to the transport configuration

##### TransportConfiguration:

* `NodePool` a registry of `Nodes` the transport will ask for a view it can iterate over.  
ONLY if a connection pool indicates it supports receiving new nodes will the transport sniff.
* `ITransportClient`  
Abstraction for the actual IO the transport needs to perform. 
* `ITransportSerializer`  
Allows you to inject your own serializer, the default uses `System.Text.Json`
* `IProductRegistration`  
Product specific implementations and metadata provider


#### Transport

* `ITransportConfigurationValues`  
A transport configuration instance, explictly designed for clients to introduce subclasses of
* `IRequestPipelineFactory`
A factory creating `IRequestPipeline` instances
* `IDateTimeProvider`
Abstraction around the static `DateTime.Now` so we can test algorithms without waiting on the clock on the wall.
* `IMemoryStreamFactory`
A factory creating `MemoryStream` instances.



### Observability

The default `ITransport` implementation ships with various `DiagnosticSources` to make the whole 
flow through the request pipeline auditable and debuggable.  

Every response returned by `Transport` has to implement `ITransportResponse` which has one property `ApiCall` of 
type `IApiCallDetails` which in turns holds all information relevant to the request and response. 

`NOTE:` it also exposes `response.ApiCall.DebugInformation` always holds a human readable string to indicate 
what happened.

Further more `DiagnosticSources` exists for various purposes e.g (de)serialization times, time to first byte & various counters







