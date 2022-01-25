// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Collections.Generic;
using Elastic.Transport.Products;
using Elastic.Transport.VirtualizedCluster.Components;

namespace Elastic.Transport.VirtualizedCluster.Products
{
	/// <summary>
	/// Makes sure <see cref="VirtualClusterConnection"/> is mockable by providing a different sniff response based on the current <see cref="ProductRegistration"/>
	/// </summary>
	public interface IMockProductRegistration
	{
		/// <summary>
		/// Information about the current product we are injecting into <see cref="Transport{TConnectionSettings}"/>
		/// </summary>
		IProductRegistration ProductRegistration { get; }

		/// <summary>
		/// Return the sniff response for the product as raw bytes for <see cref="ITransportClient.Request{TResponse}"/> to return.
		/// </summary>
		/// <param name="nodes">The nodes we expect to be returned in the response</param>
		/// <param name="stackVersion">The current version under test</param>
		/// <param name="publishAddressOverride">Return this hostname instead of some IP</param>
		/// <param name="returnFullyQualifiedDomainNames">If the sniff can return internal + external information return both</param>
		byte[] CreateSniffResponseBytes(IReadOnlyList<Node> nodes, string stackVersion, string publishAddressOverride, bool returnFullyQualifiedDomainNames);

		/// <summary>
		/// see <see cref="VirtualClusterConnection.Request{TResponse}"/> uses this to determine if the current request is a sniff request and should follow
		/// the sniffing rules
		/// </summary>
		bool IsSniffRequest(RequestData requestData);

		bool IsPingRequest(RequestData requestData);
	}
}
