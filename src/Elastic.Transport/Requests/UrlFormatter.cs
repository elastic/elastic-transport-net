// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.Linq;
using Elastic.Transport.Extensions;

namespace Elastic.Transport;

/// <summary>
/// A formatter that can utilize <see cref="ITransportConfiguration" /> to resolve <see cref="IUrlParameter" />'s passed
/// as format arguments. It also handles known string representations for e.g bool/Enums/IEnumerable.
/// </summary>
public sealed class UrlFormatter : IFormatProvider, ICustomFormatter
{
	private readonly ITransportConfiguration _settings;

	/// <inheritdoc cref="UrlFormatter"/>
	public UrlFormatter(ITransportConfiguration settings) => _settings = settings;

	/// <inheritdoc cref="ICustomFormatter.Format"/>>
	public string Format(string format, object arg, IFormatProvider formatProvider)
	{
		if (arg == null) throw new ArgumentNullException();

		if (format == "r") return arg.ToString();

		var value = CreateString(arg, _settings);
		if (value.IsNullOrEmpty() && !format.IsNullOrEmpty())
			throw new ArgumentException($"The parameter: {format} to the url is null or empty");

		return value.IsNullOrEmpty() ? string.Empty : Uri.EscapeDataString(value);
	}

	/// <inheritdoc cref="IFormatProvider.GetFormat"/>
	public object GetFormat(Type formatType) => formatType == typeof(ICustomFormatter) ? this : null;

	/// <inheritdoc cref="CreateString(object, ITransportConfiguration)"/>
	public string CreateString(object value) => CreateString(value, _settings);

	/// <summary> Creates a query string representation for <paramref name="value"/> </summary>
	public static string CreateString(object value, ITransportConfiguration settings)
	{
		switch (value)
		{
			case null: return null;
			case string s: return s;
			case string[] ss: return string.Join(",", ss);
			case Enum e: return e.GetStringValue();
			case bool b: return b ? "true" : "false";
			case DateTimeOffset offset: return offset.ToString("o");
			case IEnumerable<object> pns:
				return string.Join(",", pns.Select(o => ResolveUrlParameterOrDefault(o, settings)));
			case Array pns:
				return CreateString(ConvertArrayToEnumerable(pns), settings);
			case TimeSpan timeSpan: return timeSpan.ToTimeUnit();
			default:
				return ResolveUrlParameterOrDefault(value, settings);
		}
	}

	private static IEnumerable<object> ConvertArrayToEnumerable(Array array)
	{
		for (var i = array.GetLowerBound(0); i <= array.GetUpperBound(0); i++)
		{
			yield return array.GetValue(i);
		}
	}

	private static string ResolveUrlParameterOrDefault(object value, ITransportConfiguration settings) =>
		value is IUrlParameter urlParam ? urlParam.GetString(settings) : value.ToString();
}
