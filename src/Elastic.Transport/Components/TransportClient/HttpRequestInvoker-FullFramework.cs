// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

#if NETFRAMEWORK
using System.Net;

namespace Elastic.Transport;

/// <summary> The default <see cref="IRequestInvoker"/> implementation. Uses <see cref="HttpWebRequest" /> on the current .NET desktop framework.</summary>
public class HttpRequestInvoker : HttpWebRequestInvoker
{
    /// <summary>
    /// Create a new instance of the <see cref="HttpRequestInvoker"/>.
    /// </summary>
    public HttpRequestInvoker() { }

    /// <summary> The default TransportClient implementation. Uses <see cref="HttpWebRequest" /> on the current .NET desktop framework.</summary>
    internal HttpRequestInvoker(ResponseFactory responseFactory) : base(responseFactory) { }
}
#endif
