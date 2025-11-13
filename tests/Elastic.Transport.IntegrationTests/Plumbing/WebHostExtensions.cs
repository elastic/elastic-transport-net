// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Hosting;

namespace Elastic.Transport.IntegrationTests.Plumbing
{
	internal static class WebHostExtensions
	{
		internal static int GetServerPort(this IServer server)
		{
			var address = server.Features.Get<IServerAddressesFeature>().Addresses.First();
			var match = Regex.Match(address, @"^.+:(\d+)$");

			if (!match.Success) throw new Exception($"Unable to parse port from address: {address}");

			var port = int.TryParse(match.Groups[1].Value, out var p);
			return port ? p : throw new Exception($"Unable to parse port to integer from address: {address}");
		}

	}
}
