// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

namespace Elastic.Transport;

/// <summary>
/// 
/// </summary>
internal sealed class EmptyError : ErrorResponse
{
	public static readonly EmptyError Instance = new();

	/// <inheritdoc cref="ErrorResponse.HasError" />
	public override bool HasError() => false;
}
