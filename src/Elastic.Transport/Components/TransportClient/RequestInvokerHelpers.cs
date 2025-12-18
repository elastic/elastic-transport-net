// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Diagnostics;
using Elastic.Transport.Diagnostics;

namespace Elastic.Transport;

internal static class RequestInvokerHelpers
{
	public static void SetOtelAttributes<TResponse>(BoundConfiguration boundConfiguration, TResponse response) where TResponse : TransportResponse
	{
		if (!OpenTelemetry.CurrentSpanIsElasticTransportOwnedAndHasListeners || (!(Activity.Current?.IsAllDataRequested ?? false)))
			return;

		var attributes = boundConfiguration.ConnectionSettings.ProductRegistration.ParseOpenTelemetryAttributesFromApiCallDetails(response.ApiCallDetails);

		if (attributes is null)
			return;

		foreach (var attribute in attributes)
			_ = (Activity.Current?.SetTag(attribute.Key, attribute.Value));
	}
}
