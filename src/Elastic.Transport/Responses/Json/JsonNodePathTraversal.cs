// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Elastic.Transport;

internal static class JsonNodePathTraversal
{
	private static readonly Regex SplitRegex = new(@"(?<!\\)\.");

	public static T Get<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(JsonNode node, string path)
	{
		if (path == null || node == null) return default;

		var split = SplitRegex.Split(path);
		if (split.Length == 0) return default;

		var current = node;
		for (var i = 0; i < split.Length; i++)
		{
			if (current == null) return default;

			var key = split[i].Replace(@"\.", ".");
			var isLast = i == split.Length - 1;
			current = ResolveSegment(current, key, isLast);
		}

		return ConvertNode<T>(current);
	}

	private static JsonNode ResolveSegment(JsonNode current, string key, bool isLast)
	{
		if (key == "_arbitrary_key_")
		{
			if (current is JsonObject obj)
			{
				if (!isLast)
				{
					// Traverse into first key's value
					var first = obj.FirstOrDefault();
					return first.Value;
				}
				// Return the key name
				var firstKey = obj.FirstOrDefault();
				return firstKey.Key != null ? JsonValue.Create(firstKey.Key) : null;
			}
			return null;
		}

		if (key == "_first_" || key == "[first()]")
		{
			if (current is JsonArray arr && arr.Count > 0)
				return arr[0];
			return null;
		}

		if (key == "_last_" || key == "[last()]")
		{
			if (current is JsonArray arr && arr.Count > 0)
				return arr[arr.Count - 1];
			return null;
		}

		// Bracket index: [N]
		if (key.Length > 2 && key[0] == '[' && key[key.Length - 1] == ']')
		{
#if NETSTANDARD2_0 || NET462
			var inner = key.Substring(1, key.Length - 2);
			if (int.TryParse(inner, out var bracketIndex))
#else
			if (int.TryParse(key.AsSpan(1, key.Length - 2), out var bracketIndex))
#endif
			{
				if (current is JsonArray bArr && bracketIndex >= 0 && bracketIndex < bArr.Count)
					return bArr[bracketIndex];
				return null;
			}
		}

		// Plain numeric index
		if (int.TryParse(key, out var index))
		{
			if (current is JsonArray nArr && index >= 0 && index < nArr.Count)
				return nArr[index];
			// Also allow numeric keys on objects
			if (current is JsonObject nObj && nObj.ContainsKey(key))
				return nObj[key];
			return null;
		}

		// Object property lookup
		if (current is JsonObject propObj && propObj.ContainsKey(key))
			return propObj[key];

		return null;
	}

	private static T ConvertNode<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(JsonNode node)
	{
		if (node == null) return default;

		var targetType = typeof(T);
		if (targetType == typeof(JsonNode)) return (T)(object)node;

		if (node is JsonValue val)
		{
			// Try direct extraction for common types
			if (targetType == typeof(string))
			{
				if (val.TryGetValue<string>(out var s)) return (T)(object)s;
				// For numbers/bools stored as JsonValue, convert to string
				return (T)(object)node.ToJsonString().Trim('"');
			}
			if (targetType == typeof(int))
			{
				if (val.TryGetValue<int>(out var i)) return (T)(object)i;
				if (val.TryGetValue<long>(out var l)) return (T)(object)(int)l;
				if (val.TryGetValue<string>(out var s) && int.TryParse(s, out var parsed)) return (T)(object)parsed;
				return default;
			}
			if (targetType == typeof(long))
			{
				if (val.TryGetValue<long>(out var l)) return (T)(object)l;
				if (val.TryGetValue<int>(out var i)) return (T)(object)(long)i;
				return default;
			}
			if (targetType == typeof(double))
			{
				if (val.TryGetValue<double>(out var d)) return (T)(object)d;
				return default;
			}
			if (targetType == typeof(float))
			{
				if (val.TryGetValue<float>(out var f)) return (T)(object)f;
				if (val.TryGetValue<double>(out var d)) return (T)(object)(float)d;
				return default;
			}
			if (targetType == typeof(decimal))
			{
				if (val.TryGetValue<decimal>(out var d)) return (T)(object)d;
				return default;
			}
			if (targetType == typeof(bool))
			{
				if (val.TryGetValue<bool>(out var b)) return (T)(object)b;
				return default;
			}
			if (targetType == typeof(DateTime))
			{
				if (val.TryGetValue<DateTime>(out var dt)) return (T)(object)dt;
				if (val.TryGetValue<string>(out var s) && DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
					return (T)(object)parsed;
				return default;
			}
			if (targetType == typeof(DateTimeOffset))
			{
				if (val.TryGetValue<DateTimeOffset>(out var dto)) return (T)(object)dto;
				return default;
			}
			if (targetType == typeof(Guid))
			{
				if (val.TryGetValue<Guid>(out var g)) return (T)(object)g;
				if (val.TryGetValue<string>(out var s) && Guid.TryParse(s, out var parsed)) return (T)(object)parsed;
				return default;
			}

			// Fallback: try GetValue<T>
			try
			{
				return val.GetValue<T>();
			}
			catch
			{
				return default;
			}
		}

		// For JsonObject/JsonArray, if T is object just return as-is
		if (targetType == typeof(object)) return (T)(object)node;

		return default;
	}
}
