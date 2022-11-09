// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Collections.Generic;

namespace Elastic.Transport.Products.Elasticsearch;

internal sealed class NodeInfoHttp
{
	public IList<string> bound_address { get; set; }
	public string publish_address { get; set; }
}
