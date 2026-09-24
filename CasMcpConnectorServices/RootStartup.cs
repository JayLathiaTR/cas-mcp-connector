using AuditIntelligence.WebHost.Core;
using AuditIntelligence.WebHost.Core.Configuration.Cors;
using CasMcpConnectorServices.Auth;
using AuditIntelligence.WebHost.Core.Security;
using AuditIntelligence.WebHost.Core.Types;
using Microsoft.Net.Http.Headers;

namespace CasMcpConnectorServices;

/// <summary>
/// Composition root for the connector: registers services and configures the middleware pipeline.
/// Targeted reuse of AuditIntelligence.WebHost.Core (CIAM auth + MCP OAuth discovery) — the heavier
/// host bits (messaging, feature flags, DB) are intentionally not adopted for this POC.
/// </summary>
public sealed class RootStartup
{
    private const string CorsPolicyName = "ConnectorCors";

    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;

    public RootStartup(IConfiguration configuration, IWebHostEnvironment environment)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
    }

    public void ConfigureServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddConnectorServices(_configuration);

        // Reused auth: validate the caller's CIAM token (Authorization header) and advertise the
        // OAuth discovery metadata so a third-party MCP client is challenged to log in.
        services.ConfigureCIAMAuthentication(_configuration, TokenLocation.HTTPHeader, HeaderNames.Authorization);
        services.ConfigureMcpOAuthDiscovery(_configuration);

        CorsOptions corsOptions = _configuration.ObtainCorsConfiguration();
        string[] allowedOrigins = corsOptions.AllowedDomains.Select(domain => domain.GetLeftPart(UriPartial.Authority)).ToArray();
        services.AddCors(options => options.AddPolicy(CorsPolicyName, policy =>
        {
            if (allowedOrigins.Length > 0)
            {
                policy.WithOrigins(allowedOrigins);
            }

            policy.AllowAnyMethod().AllowAnyHeader();
        }));
    }

    public void Configure(IApplicationBuilder applicationBuilder, IWebHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(applicationBuilder);

        applicationBuilder.UseExceptionHandler();
        applicationBuilder.UseCors(CorsPolicyName);
        applicationBuilder.UseAuthentication();
        applicationBuilder.UseAuthorization();

        // Copies the validated CIAM identity (euid + token) onto the scoped request auth context.
        applicationBuilder.UseMiddleware<TokenValidationMiddleware>();
    }
}
