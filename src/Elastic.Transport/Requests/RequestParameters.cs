// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Collections.Generic;
using static Elastic.Transport.UrlFormatter;

// ReSharper disable once CheckNamespace
namespace Elastic.Transport
{
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

	internal sealed class DefaultRequestParameters : RequestParameters
	{
	}

	/// <summary>
	/// Used by the raw client to compose querystring parameters in a matter that still exposes some xmldocs
	/// You can always pass a simple NameValueCollection if you want.
	/// </summary>
	public abstract class RequestParameters
	{
		/// <summary>
		/// 
		/// </summary>
		public CustomResponseBuilder CustomResponseBuilder { get; internal set; }

		/// <summary>
		/// 
		/// </summary>
		public Dictionary<string, object> QueryString { get; internal set; } = new Dictionary<string, object>();

		/// <summary>
		/// 
		/// </summary>
		public IRequestConfiguration RequestConfiguration { get; set; }

		/// <summary>
		/// 
		/// </summary>
		public bool ContainsQueryString(string name) => QueryString != null && QueryString.ContainsKey(name);

		/// <summary>
		/// 
		/// </summary>
		public TOut GetQueryStringValue<TOut>(string name)
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
		public string GetResolvedQueryStringValue(string name, ITransportConfiguration transportConfiguration) =>
			CreateString(GetQueryStringValue<object>(name), transportConfiguration);

		/// <summary>
		/// 
		/// </summary>
		public void SetQueryString(string name, object value)
		{
			if (value == null) RemoveQueryString(name);
			else QueryString[name] = value;
		}

		/// <summary> Shortcut to <see cref="GetQueryStringValue{TOut}"/> for generated code </summary>
		protected TOut Q<TOut>(string name) => GetQueryStringValue<TOut>(name);

		/// <summary> Shortcut to <see cref="SetQueryString"/> for generated code </summary>
		protected void Q(string name, object value) => SetQueryString(name, value);

		/// <summary> Shortcut to <see cref="SetQueryString"/> for generated code </summary>
		protected void Q(string name, IStringable value) => SetQueryString(name, value.GetString());

		private void RemoveQueryString(string name)
		{
			if (!QueryString.ContainsKey(name)) return;

			QueryString.Remove(name);
		}

		/// <summary>
		/// Makes sure <see cref="RequestConfiguration"/> is set before explicitly setting <see cref="IRequestConfiguration.Accept"/>
		/// </summary>
		protected void SetAcceptHeader(string format)
		{
			if (RequestConfiguration == null)
				RequestConfiguration = new RequestConfiguration();

			RequestConfiguration.Accept = AcceptHeaderFromFormat(format);
		}

		/// <summary>
		/// 
		/// </summary>
		public string AcceptHeaderFromFormat(string format)
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
}
