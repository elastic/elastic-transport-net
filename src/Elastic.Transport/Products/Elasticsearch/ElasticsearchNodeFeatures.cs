// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Collections.Generic;
using System.Linq;

namespace Elastic.Transport.Products.Elasticsearch
{
	/// <summary>
	/// Encodes the features <see cref="ElasticsearchProductRegistration.Sniff"/> will register on
	/// <see cref="Node.Features"/>. These static strings make it easier to inspect if features are enabled
	/// using <see cref="Node.HasFeature"/>
	/// </summary>
	public static class ElasticsearchNodeFeatures
	{
		/// <summary>Indicates whether this node holds data, defaults to true when unknown/unspecified</summary>
		public const string HoldsData = "node.data";
		/// <summary>Whether HTTP is enabled on the node or not</summary>
		public const string HttpEnabled = "node.http";
		/// <summary>Indicates whether this node is allowed to run ingest pipelines, defaults to true when unknown/unspecified</summary>
		public const string IngestEnabled = "node.ingest";
		/// <summary>Indicates whether this node is master eligible, defaults to true when unknown/unspecified</summary>
		public const string MasterEligible = "node.master";

		/// <summary> The default collection of features, which enables ALL Features </summary>
		public static readonly IReadOnlyCollection<string> Default =
			new[] { HoldsData, MasterEligible, IngestEnabled, HttpEnabled }.ToList().AsReadOnly();

		/// <summary> The node only has the <see cref="MasterEligible"/> and <see cref="HttpEnabled"/> features</summary>
		public static readonly IReadOnlyCollection<string> MasterEligibleOnly =
			new[] { MasterEligible, HttpEnabled }.ToList().AsReadOnly();

		/// <summary> The node has all features EXCEPT <see cref="MasterEligible"/></summary>
		// ReSharper disable once UnusedMember.Global
		public static readonly IReadOnlyCollection<string> NotMasterEligible =
			Default.Except(new[] { MasterEligible }).ToList().AsReadOnly();

	}
}
