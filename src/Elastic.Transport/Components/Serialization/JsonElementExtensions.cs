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

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace Elastic.Transport.Extensions
{
	internal static class JsonElementExtensions
	{
		/// <summary>
		/// Fully consumes a json element representing a json object. Meaning it will attempt to unwrap all JsonElement values
		/// recursively to their actual types. This should only be used in the context of <see cref="DynamicDictionary"/> which is
		/// allowed to be slow yet convenient
		/// </summary>
		public static IDictionary<string, object> ToDictionary(this JsonElement e) =>
			e.ValueKind switch
			{
				JsonValueKind.Object => e.EnumerateObject()
					.Aggregate(new Dictionary<string, object>(), (dict, je) =>
					{
						dict.Add(je.Name, DynamicValue.ConsumeJsonElement(typeof(object), je.Value));
						return dict;
					}),
				JsonValueKind.Array => e.EnumerateArray()
					.Select((je, i) => (i, o: DynamicValue.ConsumeJsonElement(typeof(object), je)))
					.Aggregate(new Dictionary<string, object>(), (dict, t) =>
					{
						dict.Add(t.i.ToString(CultureInfo.InvariantCulture), t.o);
						return dict;
					}),
				_ => null
			};
	}
}
