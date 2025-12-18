// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using Elastic.Transport.Extensions;
using static Elastic.Transport.UrlFormatter;

// ReSharper disable once CheckNamespace
namespace Elastic.Transport;

/// <summary>
///
/// </summary>
public interface IStringable
{
	/// <summary>
	///
	/// </summary>
	/// <returns></returns>
	string GetString();
}

/// <summary>
///
/// </summary>
public sealed class DefaultRequestParameters : RequestParameters;

/// <summary>
/// Used by the raw client to compose querystring parameters in a matter that still exposes some xmldocs
/// You can always pass a simple NameValueCollection if you want.
/// </summary>
public abstract class RequestParameters
{
	/// <summary>
	///
	/// </summary>
	public Dictionary<string, object> QueryString { get; internal set; } = new();

	/// <summary>
	///
	/// </summary>
	public bool ContainsQueryString(string name) => QueryString != null && QueryString.ContainsKey(name);

	/// <summary>
	///
	/// </summary>
	public TOut? GetQueryStringValue<TOut>(string name)
	{
		if (!ContainsQueryString(name))
			return default;

		var value = QueryString[name];
		if (value == null)
			return default;

		return (TOut)value;
	}

	/// <summary>
	///
	/// </summary>
	public void SetQueryString(string name, object? value)
	{
		if (value == null) RemoveQueryString(name);
		else QueryString[name] = value;
	}

	/// <summary> Shortcut to <see cref="GetQueryStringValue{TOut}"/> for generated code </summary>
	protected TOut? Q<TOut>(string name) => GetQueryStringValue<TOut>(name);

	/// <summary> Shortcut to <see cref="SetQueryString"/> for generated code </summary>
	protected void Q(string name, object value) => SetQueryString(name, value);

	/// <summary> Shortcut to <see cref="SetQueryString"/> for generated code </summary>
	protected void Q(string name, IStringable value) => SetQueryString(name, value.GetString());

	private void RemoveQueryString(string name)
	{
		if (!QueryString.ContainsKey(name)) return;

		QueryString.Remove(name);
	}

	/// <summary> </summary>
	public virtual string CreatePathWithQueryStrings(string? path, ITransportConfiguration? globalConfig)
	{
		path ??= string.Empty;
#if NET6_0_OR_GREATER
		if (path.Contains('?'))
#else
		if (path.Contains("?"))
#endif
			throw new ArgumentException($"{nameof(path)} can not contain querystring parameters and needs to be already escaped");

		var g = globalConfig?.QueryStringParameters;
		var l = QueryString;

		if ((g == null || g.Count == 0) && (l == null || l.Count == 0)) return path;

		//create a copy of the global query string collection if needed.
		var nv = g == null ? new NameValueCollection() : new NameValueCollection(g);

		//set all querystring pairs from local `l` on the querystring collection
		var formatter = globalConfig?.UrlFormatter;
		if (formatter is not null)
			nv.UpdateFromDictionary(l, formatter);

		//if nv has no keys simply return path as provided
		if (!nv.HasKeys()) return path;

		//create string for query string collection where key and value are escaped properly.
		var queryString = nv.ToQueryString();
		path += queryString;
		return path;
	}

	/// <summary> Create the specified accept-header based on the format sent over the wire </summary>
	public string? AcceptHeaderFromFormat(string? format)
	{
		if (format == null)
			return null;

		var lowerFormat = format.ToLowerInvariant();

		switch(lowerFormat)
		{
			case "smile":
			case "yaml":
			case "cbor":
			case "json":
				return $"application/{lowerFormat}";
			default:
				return null;
		}
	}
}
