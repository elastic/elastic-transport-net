// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Elastic.Transport.Extensions
{
	internal static class Extensions
	{
		[Obsolete("Please use the overload placing the enumerated array as out")]
		internal static bool HasAny<T>(this ICollection<T> list) => list != null && list.Any();

		internal static bool HasAny<T>(this IEnumerable<T> list, out T[] enumerated)
		{
			enumerated = list == null ? null : (list as T[] ?? list.ToArray());
			return enumerated != null && enumerated.Length > 0;
		}

		internal static IEnumerable<T> DistinctBy<T, TKey>(this IEnumerable<T> items, Func<T, TKey> property) =>
			items.GroupBy(property).Select(x => x.First());

		internal static void ThrowIfEmpty<T>(this IEnumerable<T> @object, string parameterName)
		{
			var enumerated = @object == null ? null : (@object as T[] ?? @object.ToArray());
			enumerated.ThrowIfNull(parameterName);
			if (!enumerated!.Any())
				throw new ArgumentException("Argument can not be an empty collection", parameterName);
		}
		internal static void ThrowIfNull<T>(this T value, string name) where T : class
		{
			if (value == null)
				throw new ArgumentNullException(name);
		}

		internal static bool IsNullOrEmpty(this string value) => string.IsNullOrEmpty(value);

		internal static string Utf8String(this byte[] bytes) => bytes == null ? null : Encoding.UTF8.GetString(bytes, 0, bytes.Length);

		internal static string Utf8String(this MemoryStream ms)
		{
			if (ms is null)
				return null;

			if (!ms.TryGetBuffer(out var buffer) || buffer.Array is null)
				return Encoding.UTF8.GetString(ms.ToArray());

			return Encoding.UTF8.GetString(buffer.Array, buffer.Offset, buffer.Count);
		}

		internal static byte[] Utf8Bytes(this string s) => s.IsNullOrEmpty() ? null : Encoding.UTF8.GetBytes(s);

		private const long MillisecondsInAWeek = MillisecondsInADay * 7;
		private const long MillisecondsInADay = MillisecondsInAnHour * 24;
		private const long MillisecondsInAnHour = MillisecondsInAMinute * 60;
		private const long MillisecondsInAMinute = MillisecondsInASecond * 60;
		private const long MillisecondsInASecond = 1000;

		internal static string ToTimeUnit(this TimeSpan timeSpan)
		{
			var ms = timeSpan.TotalMilliseconds;
			string interval;
			double factor;

			if (ms >= MillisecondsInAWeek)
			{
				factor = ms / MillisecondsInAWeek;
				interval = "w";
			}
			else if (ms >= MillisecondsInADay)
			{
				factor = ms / MillisecondsInADay;
				interval = "d";
			}
			else if (ms >= MillisecondsInAnHour)
			{
				factor = ms / MillisecondsInAnHour;
				interval = "h";
			}
			else if (ms >= MillisecondsInAMinute)
			{
				factor = ms / MillisecondsInAMinute;
				interval = "m";
			}
			else if (ms >= MillisecondsInASecond)
			{
				factor = ms / MillisecondsInASecond;
				interval = "s";
			}
			else
			{
				factor = ms;
				interval = "ms";
			}

			return factor.ToString("0.##", CultureInfo.InvariantCulture) + interval;
		}

		internal static Exception AsAggregateOrFirst(this IEnumerable<Exception> exceptions)
		{
			var es = exceptions as Exception[] ?? exceptions?.ToArray();
			if (es == null || es.Length == 0) return null;

			return es.Length == 1 ? es[0] : new AggregateException(es);
		}

	}
}
