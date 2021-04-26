/*
 * Licensed to Elasticsearch B.V. under one or more contributor
 * license agreements. See the NOTICE file distributed with
 * this work for additional information regarding copyright
 * ownership. Elasticsearch B.V. licenses this file to you under
 * the Apache License, Version 2.0 (the "License"); you may
 * not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *    http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing,
 * software distributed under the License is distributed on an
 * "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
 * KIND, either express or implied.  See the License for the
 * specific language governing permissions and limitations
 * under the License.
 */

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Elastic.Transport.Extensions
{
	/// <summary>
	/// Cached to string extension method for enums. This is public because we expect most clients to need this.
	/// <para>This takes <see cref="EnumMemberAttribute"/> into account</para>
	/// </summary>
	public static class EnumExtensions
	{
		internal static string GetStringValue(this HttpMethod enumValue)
		{
			switch (enumValue)
			{
				case HttpMethod.GET: return "GET";
				case HttpMethod.POST: return "POST";
				case HttpMethod.PUT: return "PUT";
				case HttpMethod.DELETE: return "DELETE";
				case HttpMethod.HEAD: return "HEAD";
				default:
					throw new ArgumentOutOfRangeException(nameof(enumValue), enumValue, null);
			}
		}

		private static readonly ConcurrentDictionary<Type, Func<Enum, string>> EnumStringResolvers = new ConcurrentDictionary<Type, Func<Enum, string>>();

		/// <summary>
		/// Returns the string representation of the enum taking into account <see cref="EnumMemberAttribute"/>
		/// </summary>
		public static string GetStringValue(this Enum e)
		{
			var type = e.GetType();
			var resolver = EnumStringResolvers.GetOrAdd(type, t => GetEnumStringResolver(t));
			return resolver(e);
		}

		private static Func<Enum, string> GetEnumStringResolver(Type type)
		{
			var values = Enum.GetValues(type);
			var dictionary = new Dictionary<Enum, string>(values.Length);
			for (var index = 0; index < values.Length; index++)
			{
				var value = values.GetValue(index);
				var info = type.GetField(value.ToString());
				var da = (EnumMemberAttribute[])info.GetCustomAttributes(typeof(EnumMemberAttribute), false);
				var stringValue = da.Length > 0 ? da[0].Value : Enum.GetName(type, value);
				dictionary.Add((Enum)value, stringValue);
			}

			var isFlag = type.GetCustomAttributes(typeof(FlagsAttribute), false).Length > 0;
			return e =>
			{
				if (!isFlag) return dictionary[e];

				var list = new List<string>();
				foreach (var kv in dictionary)
				{
					if (e.HasFlag(kv.Key))
						list.Add(kv.Value);
				}

				return string.Join(",", list);
			};
		}
	}
}
