// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

namespace Elastic.Transport.VirtualizedCluster.Extensions;

internal static class NumericExtensions
{
	public static string ToOrdinal(this int num)
	{
		if (num <= 0)
			return num.ToString();

		return (num % 100) switch
		{
			11 or 12 or 13 => num + "th",
			_ => (num % 10) switch
			{
				1 => num + "st",
				2 => num + "nd",
				3 => num + "rd",
				_ => num + "th",
			}
		};
	}
}
