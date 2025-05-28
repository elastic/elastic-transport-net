// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Elastic.Transport.Products.Elasticsearch;

namespace Elastic.Transport;

/// <summary>
/// Default low level request/response-serializer implementation for <see cref="Serializer"/> which serializes using
/// the Microsoft <c>System.Text.Json</c> library
/// </summary>
internal sealed class LowLevelRequestResponseSerializer : SystemTextJsonSerializer
{
	/// <summary>
	/// Provides a static reusable reference to an instance of <see cref="LowLevelRequestResponseSerializer"/> to promote reuse.
	/// </summary>
	internal static readonly LowLevelRequestResponseSerializer Instance = new();

	/// <inheritdoc cref="LowLevelRequestResponseSerializer"/>>
	public LowLevelRequestResponseSerializer() : this(null) { }

	/// <summary>
	/// <inheritdoc cref="LowLevelRequestResponseSerializer"/>>
	/// </summary>
	/// <param name="converters">Add more default converters onto <see cref="JsonSerializerOptions"/> being used</param>
	//[RequiresUnreferencedCode(JsonSerializer.SerializationUnreferencedCodeMessage)]
	//[RequiresDynamicCode(JsonSerializer.SerializationRequiresDynamicCodeMessage)]

	[UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026:RequiresUnreferencedCode", Justification = "We always provide a static JsonTypeInfoResolver")]
	[UnconditionalSuppressMessage("AotAnalysis", "IL3050:RequiresDynamicCode", Justification = "We always provide a static JsonTypeInfoResolver")]
	public LowLevelRequestResponseSerializer(IReadOnlyCollection<JsonConverter>? converters)
		: base(new TransportSerializerOptionsProvider([
			new ExceptionConverter(),
			new ErrorCauseConverter(),
			new ErrorConverter(),
			new DynamicDictionaryConverter()
		], converters, options =>
		{
			options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
			options.TypeInfoResolver = JsonTypeInfoResolver.Combine(new DefaultJsonTypeInfoResolver(), ElasticsearchTransportSerializerContext.Default);
		})) { }
}
