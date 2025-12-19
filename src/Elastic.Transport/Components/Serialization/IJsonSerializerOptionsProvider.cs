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
/// altering the shared <see cref="JsonSerializerOptions"/> used by <see cref="SystemTextJsonSerializer"/> and its derived classes
/// </summary>
public class TransportSerializerOptionsProvider : IJsonSerializerOptionsProvider
{
	private readonly IReadOnlyCollection<JsonConverter>? _bakedInConverters;
	private readonly IReadOnlyCollection<JsonConverter>? _userProvidedConverters;
	private readonly Action<JsonSerializerOptions>? _mutateOptions;

	/// <inheritdoc cref="IJsonSerializerOptionsProvider"/>
	public JsonSerializerOptions CreateJsonSerializerOptions()
	{
		var options = new JsonSerializerOptions();

		foreach (var converter in _bakedInConverters ?? [])
			options.Converters.Add(converter);

		foreach (var converter in _userProvidedConverters ?? [])
			options.Converters.Add(converter);

		_mutateOptions?.Invoke(options);

		return options;
	}

	/// <inheritdoc cref="TransportSerializerOptionsProvider"/>
	public TransportSerializerOptionsProvider() { }

	/// <inheritdoc cref="TransportSerializerOptionsProvider"/>
	public TransportSerializerOptionsProvider(
		IReadOnlyCollection<JsonConverter> bakedInConverters,
		IReadOnlyCollection<JsonConverter>? userProvidedConverters,
		Action<JsonSerializerOptions>? mutateOptions = null
	)
	{
		_bakedInConverters = bakedInConverters;
		_userProvidedConverters = userProvidedConverters;
		_mutateOptions = mutateOptions;
	}
}
