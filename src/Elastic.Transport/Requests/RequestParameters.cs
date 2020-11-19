// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Collections.Generic;
using static Elastic.Transport.UrlFormatter;

// ReSharper disable once CheckNamespace
namespace Elastic.Transport
{

	/// <inheritdoc cref="IRequestParameters"/>
	public class RequestParameters : RequestParameters<RequestParameters>
	{
	}

	/// <summary>
	/// Used by the raw client to compose querystring parameters in a matter that still exposes some xmldocs
	/// You can always pass a simple NameValueCollection if you want.
	/// </summary>
	/// <typeparam name="T"></typeparam>
	public abstract class RequestParameters<T> : IRequestParameters where T : RequestParameters<T>
	{
		/// <inheritdoc cref="IRequestParameters.CustomResponseBuilder"/>
		public CustomResponseBuilderBase CustomResponseBuilder { get; set; }

		/// <inheritdoc cref="IRequestParameters.QueryString"/>
		public Dictionary<string, object> QueryString { get; set; } = new Dictionary<string, object>();

		/// <inheritdoc cref="IRequestParameters.RequestConfiguration"/>
		public IRequestConfiguration RequestConfiguration { get; set; }

		private IRequestParameters Self => this;

		/// <inheritdoc />
		public bool ContainsQueryString(string name) => Self.QueryString != null && Self.QueryString.ContainsKey(name);

		/// <inheritdoc />
		public TOut GetQueryStringValue<TOut>(string name)
		{
			if (!ContainsQueryString(name))
				return default;

			var value = Self.QueryString[name];
			if (value == null)
				return default;

			return (TOut)value;
		}

		/// <inheritdoc />
		public string GetResolvedQueryStringValue(string name, ITransportConfiguration transportConfiguration) =>
			CreateString(GetQueryStringValue<object>(name), transportConfiguration);

		/// <inheritdoc />
		public void SetQueryString(string name, object value)
		{
			if (value == null) RemoveQueryString(name);
			else Self.QueryString[name] = value;
		}

		/// <summary> Shortcut to <see cref="GetQueryStringValue{TOut}"/> for generated code </summary>
		protected TOut Q<TOut>(string name) => GetQueryStringValue<TOut>(name);

		/// <summary> Shortcut to <see cref="SetQueryString"/> for generated code </summary>
		protected void Q(string name, object value) => SetQueryString(name, value);

		private void RemoveQueryString(string name)
		{
			if (!Self.QueryString.ContainsKey(name)) return;

			Self.QueryString.Remove(name);
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

		/// <inheritdoc />
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
