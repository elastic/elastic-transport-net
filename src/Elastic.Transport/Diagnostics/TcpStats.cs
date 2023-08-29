// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net.NetworkInformation;

namespace Elastic.Transport.Diagnostics;

/// <summary>
/// Gets statistics about TCP connections
/// </summary>
internal static class TcpStats
{
	private static readonly int StateLength = Enum.GetNames(typeof(TcpState)).Length;
	private static readonly ReadOnlyDictionary<TcpState, int> Empty = new(new Dictionary<TcpState, int>());

	/// <summary>
	/// Gets the active TCP connections
	/// </summary>
	/// <returns>TcpConnectionInformation[]</returns>
	/// <remarks>Can return `null` when there is a permissions issue retrieving TCP connections.</remarks>
	public static TcpConnectionInformation[]? GetActiveTcpConnections()
	{
		try
		{
			return IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpConnections();
		}
		catch (NetworkInformationException) // host might not allow this information to be fetched.
		{
			// ignored
		}

		return null;			
	}

	/// <summary>
	/// Gets the sum for each state of the active TCP connections
	/// </summary>
	public static ReadOnlyDictionary<TcpState, int> GetStates()
	{
		var connections = GetActiveTcpConnections();
		if (connections is null)
		{
			return Empty;
		}

		var states = new Dictionary<TcpState, int>(StateLength);

		for (var index = 0; index < connections.Length; index++)
		{
			var connection = connections[index];
			if (states.TryGetValue(connection.State, out var count))
				states[connection.State] = ++count;
			else
				states.Add(connection.State, 1);
		}

		return new ReadOnlyDictionary<TcpState, int>(states);
	}

	/// <summary>
	/// Gets the TCP statistics for a given network interface component
	/// </summary>
	public static TcpStatistics GetTcpStatistics(NetworkInterfaceComponent version)
	{
		var properties = IPGlobalProperties.GetIPGlobalProperties();
		switch (version)
		{
			case NetworkInterfaceComponent.IPv4:
				return properties.GetTcpIPv4Statistics();
			case NetworkInterfaceComponent.IPv6:
				return properties.GetTcpIPv6Statistics();
			default:
				throw new ArgumentException("version");
		}
	}
}
