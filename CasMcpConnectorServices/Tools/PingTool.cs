using System.ComponentModel;
using ModelContextProtocol.Server;

namespace CasMcpConnectorServices.Tools;

/// <summary>
/// Placeholder tool that proves the MCP server + tool wiring end-to-end (list + call).
/// Removed once the first real tool (Engagement Contents) lands in P0.
/// </summary>
[McpServerToolType]
public sealed class PingTool
{
    [McpServerTool(Name = "server_ping")]
    [Description("Health probe: returns the connector name, status and server time (UTC). Scaffolding placeholder.")]
    public static PingResult Ping() => new("cas-mcp-connector", "ok", DateTime.UtcNow.ToString("o"));

    /// <summary>Shape returned by <see cref="Ping"/>.</summary>
    public sealed record PingResult(string Service, string Status, string ServerTimeUtc);
}
