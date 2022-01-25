// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

#if !DOTNETCORE
using System.Net;

namespace Elastic.Transport
{
	/// <summary> The default ITransportClient implementation. Uses <see cref="HttpWebRequest" /> on the current .NET desktop framework.</summary>
	public class HttpTransportClient : HttpWebRequestTransportClient { }
}
#endif
