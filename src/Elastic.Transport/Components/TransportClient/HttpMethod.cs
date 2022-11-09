// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Runtime.Serialization;

// ReSharper disable InconsistentNaming

namespace Elastic.Transport;

/// <summary> Http Method of the API call to be performed </summary>
public enum HttpMethod
{
	[EnumMember(Value = "GET")]
// These really do not need xmldocs, leave it to the reader if they feel inspired :)
#pragma warning disable 1591
	GET,

	[EnumMember(Value = "POST")]
	POST,

	[EnumMember(Value = "PUT")]
	PUT,

	[EnumMember(Value = "DELETE")]
	DELETE,

	[EnumMember(Value = "HEAD")]
	HEAD
#pragma warning restore 1591
}
