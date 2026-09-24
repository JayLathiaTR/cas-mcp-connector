namespace CasMcpConnectorServices.Configuration;

/// <summary>
/// Base URLs for the downstream services this connector calls. Bound to the single
/// <c>DownstreamServices</c> configuration section — each service maps directly to its base URL,
/// so adding a new one (e.g. <c>EngagementManagerV2</c>) is one extra line with no repeated wrappers.
/// EM-only (no GA).
/// </summary>
public sealed class DownstreamServicesOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "DownstreamServices";

    /// <summary>
    /// Engagement Manager V1 web-services host
    /// (e.g. <c>https://webservices-demo.engagementmanager.thomsonreuters.com</c>).
    /// </summary>
    public string EngagementManagerV1 { get; init; } = string.Empty;
}
