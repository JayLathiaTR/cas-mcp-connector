namespace CasMcpConnectorServices.Configuration;

/// <summary>
/// Base URLs for the downstream services this connector calls. Bound to the single
/// <c>DownstreamServices</c> configuration section — each service maps directly to its base URL,
/// so adding a new one is one extra line with no repeated wrappers.
/// </summary>
public sealed class DownstreamServicesOptions
{
    public const string SectionName = "DownstreamServices";

    /// <summary>Engagement Manager V1 web-services host.</summary>
    public string EngagementManagerV1 { get; init; } = string.Empty;

    /// <summary>GoFileRoom host, used for the CIAM to GFR token exchange.</summary>
    public string GoFileRoom { get; init; } = string.Empty;
}
