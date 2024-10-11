// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

using Elastic.Transport.Extensions;

namespace Elastic.Transport;

/// <summary>
/// Default low level request/response-serializer implementation for <see cref="Serializer"/> which serializes using
/// the Microsoft <c>System.Text.Json</c> library
/// </summary>
internal sealed class LowLevelRequestResponseSerializer :
	SystemTextJsonSerializer
{
	/// <summary>
	/// Provides a static reusable reference to an instance of <see cref="LowLevelRequestResponseSerializer"/> to promote reuse.
	/// </summary>
	internal static readonly LowLevelRequestResponseSerializer Instance = new();

	private IReadOnlyCollection<JsonConverter> AdditionalConverters { get; }

	private IList<JsonConverter> BakedInConverters { get; } = new List<JsonConverter>
		{
			new ExceptionConverter(),
			new ErrorCauseConverter(),
			new ErrorConverter(),
			new DynamicDictionaryConverter()
		};

	/// <inheritdoc cref="LowLevelRequestResponseSerializer"/>>
	public LowLevelRequestResponseSerializer() : this(null) { }

	/// <summary>
	/// <inheritdoc cref="LowLevelRequestResponseSerializer"/>>
	/// </summary>
	/// <param name="converters">Add more default converters onto <see cref="JsonSerializerOptions"/> being used</param>
	public LowLevelRequestResponseSerializer(IEnumerable<JsonConverter>? converters) =>
		AdditionalConverters = converters != null
			? new ReadOnlyCollection<JsonConverter>(converters.ToList())
			: EmptyReadOnly<JsonConverter>.Collection;

	/// <summary>
	/// Creates <see cref="JsonSerializerOptions"/> used for serialization.
	/// Override on a derived serializer to change serialization.
	/// </summary>
	protected override JsonSerializerOptions? CreateJsonSerializerOptions()
	{
		var options = new JsonSerializerOptions
		{
			DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
		};

		foreach (var converter in BakedInConverters)
			options.Converters.Add(converter);

		foreach (var converter in AdditionalConverters)
			options.Converters.Add(converter);

		return options;
	}
}
