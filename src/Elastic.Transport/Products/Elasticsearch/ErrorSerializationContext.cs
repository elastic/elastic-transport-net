// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Elastic.Transport.Products.Elasticsearch;

/// Adds support for serializing Elasticsearch errors using <see cref="IJsonTypeInfoResolver"/>
[JsonSerializable(typeof(Error))]
[JsonSerializable(typeof(ErrorCause))]
[JsonSerializable(typeof(ElasticsearchServerError))]
[JsonSerializable(typeof(ElasticsearchResponse))]
public partial class ErrorSerializerContext : JsonSerializerContext;
