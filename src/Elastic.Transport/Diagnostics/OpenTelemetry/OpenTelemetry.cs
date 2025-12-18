// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Diagnostics;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Elastic.Transport.Diagnostics;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Activity information for OpenTelemetry instrumentation.
/// </summary>
public static class OpenTelemetry
{
	// This should be updated if any of the code uses semantic conventions defined in newer schema versions.
	internal const string OpenTelemetrySchemaVersion = "https://opentelemetry.io/schemas/1.21.0";

	// This is hard-coded, for now, but could later be exposed as a configuration, if required.
	internal const int MinimumMillisecondsToEmitTimingSpanAttribute = 20;

	/// <summary>
	/// The name of the primary <see cref="ActivitySource"/> for the transport. 
	/// </summary>
	public const string ElasticTransportActivitySourceName = "Elastic.Transport";

	internal static ActivitySource ElasticTransportActivitySource = new(ElasticTransportActivitySourceName, "1.0.0");

	/// <summary>
	/// Check if the "Elastic.Transport" <see cref="ActivitySource"/> has listeners.
	/// Allows derived clients to avoid overhead to collect attributes when there are no listeners.
	/// </summary>
	public static bool ElasticTransportActivitySourceHasListeners => ElasticTransportActivitySource.HasListeners();

	internal static bool CurrentSpanIsElasticTransportOwnedAndHasListeners => ElasticTransportActivitySource.HasListeners() &&
		(Activity.Current?.Source.Name.Equals(ElasticTransportActivitySourceName, StringComparison.Ordinal) ?? false);

	internal static bool CurrentSpanIsElasticTransportOwnedHasListenersAndAllDataRequested => ElasticTransportActivitySource.HasListeners() &&
		(Activity.Current?.Source.Name.Equals(ElasticTransportActivitySourceName, StringComparison.Ordinal) ?? false) && (Activity.Current?.IsAllDataRequested ?? false);

	internal static void SetCommonAttributes(Activity? activity, ITransportConfiguration settings)
	{
		if (activity is null)
			return;

		if (settings.ProductRegistration.DefaultOpenTelemetryAttributes is not null)
		{
			foreach (var attribute in settings.ProductRegistration.DefaultOpenTelemetryAttributes)
			{
				_ = (activity?.SetTag(attribute.Key, attribute.Value));
			}
		}

		var productSchemaVersion = string.Empty;
		foreach (var attribute in activity!.TagObjects)
		{
			if (attribute.Key.Equals(OpenTelemetryAttributes.DbElasticsearchSchemaUrl, StringComparison.Ordinal))
			{
				if (attribute.Value is string schemaVersion)
					productSchemaVersion = schemaVersion;
			}
		}

		// We add the client schema version only when it differs from the product schema version
		if (!productSchemaVersion.Equals(OpenTelemetrySchemaVersion, StringComparison.Ordinal))
			_ = (activity?.SetTag(OpenTelemetryAttributes.ElasticTransportSchemaVersion, OpenTelemetrySchemaVersion));
	}
}
