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
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Elastic.Transport
{
	internal class DynamicDictionaryConverter : JsonConverter<DynamicDictionary>
	{
		public override DynamicDictionary Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.StartArray)
			{
				var array = JsonSerializer.Deserialize<object[]>(ref reader, options);
				var arrayDict = new Dictionary<string, object>();
				for (var i = 0; i < array.Length; i++)
					arrayDict[i.ToString(CultureInfo.InvariantCulture)] = new DynamicValue(array[i]);
				return DynamicDictionary.Create(arrayDict);
			}
			if (reader.TokenType != JsonTokenType.StartObject) throw new JsonException();

			var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(ref reader, options);
			return DynamicDictionary.Create(dict);
		}

		public override void Write(Utf8JsonWriter writer, DynamicDictionary dictionary, JsonSerializerOptions options)
		{
			writer.WriteStartObject();

			foreach (var kvp in dictionary)
			{
				if (kvp.Value == null) continue;

				writer.WritePropertyName(kvp.Key);

				JsonSerializer.Serialize(writer, kvp.Value?.Value, options);
			}

			writer.WriteEndObject();
		}
	}
}
