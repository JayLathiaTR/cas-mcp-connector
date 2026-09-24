namespace CasMcpConnectorServices.EngagementManager;

/// <summary>
/// Absolute paths for the Engagement Manager V1 web-services host.
/// </summary>
public static class EngagementManagerEndpoints
{
    /// <summary>All items (workpapers and folders) contained in an engagement.</summary>
    public static string EngagementItems(string engagementId) =>
        $"/Api/EngagementManagement/Binder/v4/{engagementId}/Items";
}
