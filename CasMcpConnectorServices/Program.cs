using CasMcpConnectorServices.Tools;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// MCP server over streamable HTTP. Tools are registered explicitly (additive).
builder.Services
    .AddMcpServer(options => options.ServerInfo = new() { Name = "cas-mcp-connector", Version = "0.1.0" })
    .WithHttpTransport()
    .WithTools<PingTool>();

WebApplication app = builder.Build();

// The MCP endpoint. Authorization/OAuth discovery is layered on in the auth phase (P1).
app.MapMcp("/mcp");

app.Run();

/// <summary>Exposed so the test host (WebApplicationFactory) can reference the entry point.</summary>
public partial class Program;
