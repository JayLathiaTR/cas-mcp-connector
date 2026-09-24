using System.ComponentModel;
using CasMcpConnectorServices.EngagementManager;
using ModelContextProtocol.Server;

namespace CasMcpConnectorServices.Tools;

/// <summary>
/// MCP tool that reads the contents (workpapers and folders) of a single engagement from
/// Engagement Manager. Exposes <c>em_list_engagement_workpapers</c>.
/// </summary>
[McpServerToolType]
public sealed class EngagementContentsTool
{
    private readonly EngagementManagerClient _emClient;

    public EngagementContentsTool(EngagementManagerClient emClient)
    {
        _emClient = emClient ?? throw new ArgumentNullException(nameof(emClient));
    }

    [McpServerTool(Name = "em_list_engagement_workpapers")]
    [Description(
        "Engagement Manager tool. Returns all items (workpapers and folders) contained in an engagement — " +
        "the full engagement tree, with the numeric workpaperId of each item. " +
        "DISPLAY: present as a TREE (folders as parents with their workpapers nested underneath).")]
    public async Task<object?> ListEngagementContents(
        [Description("The Engagement Manager engagement id (engagementManagerEngagementId).")] string engagementManagerEngagementId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _emClient
                .GetEngagementItemsAsync(engagementManagerEngagementId, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // POC-level error surfacing: return a structured error the client can read.
            return new
            {
                error = ex.Message,
                tool = "em_list_engagement_workpapers",
                engagementManagerEngagementId,
            };
        }
    }
}
