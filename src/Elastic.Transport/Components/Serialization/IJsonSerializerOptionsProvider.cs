// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Elastic.Transport;

/// <summary>
/// Provides an instance of <see cref="JsonSerializerOptions"/> to <see cref="SystemTextJsonSerializer"/>
/// </summary>
public interface IJsonSerializerOptionsProvider
{
	/// <inheritdoc cref="IJsonSerializerOptionsProvider"/>
	JsonSerializerOptions CreateJsonSerializerOptions();
}
/// <summary>
/// Default implementation of <see cref="IJsonSerializerOptionsProvider"/> specialized in providing more converters and
/// altering the shared <see cref="JsonSerializerOptions"/> used by <see cref="SystemTextJsonSerializer"/> and its derrived classes
/// </summary>
public class TransportSerializerOptionsProvider : IJsonSerializerOptionsProvider
{
	private readonly JsonSerializerOptions _options = new();

	/// <inheritdoc cref="IJsonSerializerOptionsProvider"/>
	public JsonSerializerOptions? CreateJsonSerializerOptions() => _options;

	/// <inheritdoc cref="TransportSerializerOptionsProvider"/>
	public TransportSerializerOptionsProvider() { }

	/// <inheritdoc cref="TransportSerializerOptionsProvider"/>
	public TransportSerializerOptionsProvider(IReadOnlyCollection<JsonConverter> bakedIn, IReadOnlyCollection<JsonConverter>? userProvided, Action<JsonSerializerOptions>? optionsAction = null)
	{
		foreach (var converter in bakedIn)
			_options.Converters.Add(converter);

		foreach (var converter in userProvided ?? [])
			_options.Converters.Add(converter);

		optionsAction?.Invoke(_options);

	}
}

