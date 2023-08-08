using System.Diagnostics;

namespace Elastic.Transport.Diagnostics;

/// <summary>
/// Activity information for OpenTelemetry instrumentation.
/// </summary>
public static class OpenTelemetry
{
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
}
