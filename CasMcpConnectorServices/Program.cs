using AuditIntelligence.WebHost.Core;
using AuditIntelligence.WebHost.Core.Configuration.Authentication;
using AuditIntelligence.WebHost.Core.Configuration.EnvironmentVariable;
using AuditIntelligence.WebHost.Core.Configuration.Serilog;
using AuditIntelligence.WebHost.Core.Enumerators;
using AuditIntelligence.WebHost.Core.Extensions;
using AuditIntelligence.WebHost.Core.Security;
using AuditIntelligence.WebHost.Core.Types;
using CasMcpConnectorServices;
using Microsoft.Extensions.Options;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Targeted reuse: no databases wired in P1a (empty connection-string key map). Postgres arrives in P1b.
builder.Host
    .RegisterConfigurationsAndSecrets(new Dictionary<KnownDatabaseServerNames, SupportedRelationalDatabases>())
    .ConfigureSerilog()
    .ConfigureOverrideEnvironmentVariables();

RootStartup startup = new(builder.Configuration, builder.Environment);
startup.ConfigureServices(builder.Services);

WebApplication app = builder.Build();
startup.Configure(app, app.Environment);

// Require auth on the MCP endpoint, so an unauthenticated/expired call is challenged with the
// protected-resource metadata (WWW-Authenticate) that points a client at CIAM for login.
string mcpEndpointPath = app.Services.GetRequiredService<IOptions<McpAuthOptions>>().Value.McpEndpointPath;
app.MapMcp(mcpEndpointPath).RequireAuthorization(ConfigureMcpOAuthDiscoveryExtensions.DiscoveryAuthorizationPolicy);

app.Run();

/// <summary>Exposed so the test host (WebApplicationFactory) can reference the entry point.</summary>
public partial class Program;
