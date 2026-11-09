// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

namespace Elastic.Transport;

/// <summary>
/// Implemented by mutable configurations (the descriptors). A transport locks the configuration it receives, so a change made
/// after that point fails instead of being applied to some requests and not others. Internal on purpose: it is not an extension point.
/// </summary>
internal interface ILockableConfiguration
{
	/// <summary>Rejects every change from now on. Locking twice is fine.</summary>
	void Lock();
}
