// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections;
using System.Globalization;
using System.Text;

using Elastic.Transport.Extensions;

namespace Elastic.Transport;

/// <summary>
/// A formatter that can utilize <see cref="ITransportConfiguration" /> to resolve <see cref="IUrlParameter" />'s passed
/// as format arguments. It also handles known string representations for e.g. bool/Enums/IEnumerable.
/// </summary>
/// <inheritdoc cref="UrlFormatter"/>
public sealed class UrlFormatter(ITransportConfiguration settings) : IFormatProvider, ICustomFormatter
{
	private readonly ITransportConfiguration _settings = settings;

	/// <inheritdoc cref="ICustomFormatter.Format"/>>
	public string Format(string? format, object? arg, IFormatProvider? formatProvider)
	{
		if (arg is null)
			throw new ArgumentNullException(nameof(arg));

		if (format == "r")
			return arg.ToString() ?? string.Empty;

		var value = CreateString(arg, _settings);
		if (value.IsNullOrEmpty() && !format.IsNullOrEmpty())
			throw new ArgumentException($"The parameter: {format} to the url is null or empty");

		return string.IsNullOrEmpty(value) ? string.Empty : Uri.EscapeDataString(value);
	}

	/// <inheritdoc cref="IFormatProvider.GetFormat"/>
	public object? GetFormat(Type? formatType) => formatType == typeof(ICustomFormatter) ? this : null;

	/// <inheritdoc cref="CreateString(object, ITransportConfiguration)"/>
	public string CreateString(object? value) => CreateString(value, _settings);

	/// <summary> Creates a query string representation for <paramref name="value"/> </summary>
	public static string CreateString(object? value, ITransportConfiguration settings) =>
		value switch
		{
			null => string.Empty,
			string s => s,
			string[] ss => string.Join(",", ss),
			Enum e => e.GetEnumName(),
			bool b => b ? "true" : "false",
			DateTimeOffset offset => offset.ToString("o"),
			TimeSpan timeSpan => timeSpan.ToTimeUnit(),
			// Custom `IUrlParameter.GetString()` implementations should take precedence over collection
			// specializations.
			IUrlParameter urlParam => urlParam.GetString(settings),
			// Special handling to support non-zero based arrays
			Array pns => CreateStringFromArray(pns, settings),
			// Performance optimization for directly indexable collections
			IList pns => CreateStringFromIList(pns, settings),
			// Generic implementation for all other collections
			IEnumerable pns => CreateStringFromIEnumerable(pns, settings),
			// Generic implementation for `IFormattable` types
			IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
			// Last resort fallback
			_ => value.ToString() ?? string.Empty
		};

	private static string CreateStringFromArray(Array value, ITransportConfiguration settings)
	{
		switch (value.Length)
		{
			case 0:
				return string.Empty;

			case 1:
				return CreateString(value.GetValue(value.GetLowerBound(0)), settings);
		}

		var sb = new StringBuilder();

		for (var i = value.GetLowerBound(0); i <= value.GetUpperBound(0); ++i)
		{
			if (sb.Length != 0)
				_ = sb.Append(',');

			_ = sb.Append(CreateString(value.GetValue(i), settings));
		}

		return sb.ToString();
	}

	private static string CreateStringFromIList(IList value, ITransportConfiguration settings)
	{
		switch (value.Count)
		{
			case 0:
				return string.Empty;

			case 1:
				return CreateString(value[0], settings);
		}

		var sb = new StringBuilder();

		for (var i = 0; i < value.Count; ++i)
		{
			if (sb.Length != 0)
				_ = sb.Append(',');

			_ = sb.Append(CreateString(value[i], settings));
		}

		return sb.ToString();
	}

	private static string CreateStringFromIEnumerable(IEnumerable value, ITransportConfiguration settings)
	{
		var sb = new StringBuilder();

		foreach (var v in value)
		{
			if (sb.Length != 0)
				_ = sb.Append(',');

			_ = sb.Append(CreateString(v, settings));
		}

		return sb.ToString();
	}
}
