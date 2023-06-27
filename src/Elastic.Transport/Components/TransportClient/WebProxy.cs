// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

#if !NETFRAMEWORK
using System;
using System.Net;

namespace Elastic.Transport;

internal class WebProxy : IWebProxy
{
	private readonly Uri _uri;

	public WebProxy(Uri uri) => _uri = uri;

	public ICredentials Credentials { get; set; }

	public Uri GetProxy(Uri destination) => _uri;

	public bool IsBypassed(Uri host) => host.IsLoopback;
}
#endif
