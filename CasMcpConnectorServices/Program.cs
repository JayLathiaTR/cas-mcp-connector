using CasMcpConnectorServices.Configuration;
using CasMcpConnectorServices.EngagementManager;
using CasMcpConnectorServices.Tools;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Needed so the EM client can read the inbound Authorization header (P0 forwarding shim).
builder.Services.AddHttpContextAccessor();

builder.Services.Configure<DownstreamServicesOptions>(
    builder.Configuration.GetSection(DownstreamServicesOptions.SectionName));

// Typed client for the EM V1 downstream. Base URL from config; Authorization is added per-request.
builder.Services.AddHttpClient<EngagementManagerClient>((serviceProvider, client) =>
{
    DownstreamServicesOptions options = serviceProvider.GetRequiredService<IOptions<DownstreamServicesOptions>>().Value;
    if (!string.IsNullOrEmpty(options.EngagementManagerV1))
    {
        client.BaseAddress = new Uri(options.EngagementManagerV1);
    }

    client.DefaultRequestHeaders.TryAddWithoutValidation(HeaderNames.Accept, "application/json");
});

// MCP server over streamable HTTP. Tools are registered explicitly (additive).
builder.Services
    .AddMcpServer(options => options.ServerInfo = new() { Name = "cas-mcp-connector", Version = "0.1.0" })
    .WithHttpTransport()
    .WithTools<PingTool>()
    .WithTools<EngagementContentsTool>();

WebApplication app = builder.Build();

// The MCP endpoint. Authorization/OAuth discovery is layered on in the auth phase (P1).
app.MapMcp("/mcp");

app.Run();

/// <summary>Exposed so the test host (WebApplicationFactory) can reference the entry point.</summary>
public partial class Program;
