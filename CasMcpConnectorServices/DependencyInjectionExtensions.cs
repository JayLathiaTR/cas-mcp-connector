using CasMcpConnectorServices.Auth.OAuth;
using CasMcpConnectorServices.Configuration;
using CasMcpConnectorServices.DataAccess;
using CasMcpConnectorServices.EngagementManager;
using CasMcpConnectorServices.GoFileRoom;
using CasMcpConnectorServices.Security;
using CasMcpConnectorServices.Tools;
using AuditIntelligence.WebHost.Core.Configuration.Postgresql;
using AuditIntelligence.WebHost.Core.Enumerators;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.AspNetCore.Authentication;
using Npgsql;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace CasMcpConnectorServices;

/// <summary>Registers the connector's own services (config, token store, GFR exchange, EM client, MCP tools).</summary>
public static class DependencyInjectionExtensions
{
    public static IServiceCollection AddConnectorServices(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddProblemDetails();

        // Mock OAuth authorization server (POC): our own login page + code store, and repoint the MCP
        // discovery's authorization_servers at THIS connector instead of CIAM.
        services.Configure<ConnectorOAuthOptions>(configuration.GetSection(ConnectorOAuthOptions.SectionName));
        services.AddSingleton<OAuthCodeStore>();
        // Advertise THIS connector as the authorization server, derived from the incoming request so it
        // works on any host/port (5080 F5, 7020 docker, ...) without hardcoding.
        services.PostConfigure<McpAuthenticationOptions>(McpAuthenticationDefaults.AuthenticationScheme, mcpOptions =>
        {
            mcpOptions.Events.OnResourceMetadataRequest = context =>
            {
                HttpRequest request = context.HttpContext.Request;
                string baseUrl = $"{request.Scheme}://{request.Host}";
                context.ResourceMetadata!.AuthorizationServers.Clear();
                context.ResourceMetadata.AuthorizationServers.Add(baseUrl);
                return Task.CompletedTask;
            };
        });

        services.Configure<DownstreamServicesOptions>(configuration.GetSection(DownstreamServicesOptions.SectionName));
        services.Configure<TokenEncryptionOptions>(configuration.GetSection(TokenEncryptionOptions.SectionName));

        // Encrypted GFR-token store (Postgres) + per-request auth context.
        NpgsqlDataSource dataSource = new NpgsqlDataSourceBuilder(
            configuration.ObtainPostgresqlConnectionString(KnownDatabaseServerNames.PrimaryDb)).Build();
        services.AddSingleton(dataSource);
        services.AddDbContext<ConnectorDbContext>((serviceProvider, options) =>
            options.UseNpgsql(serviceProvider.GetRequiredService<NpgsqlDataSource>()));
        services.AddSingleton<IRecordEncryptor, AesGcmRecordEncryptor>();
        services.AddScoped<IRequestAuthContext, RequestAuthContext>();

        // CIAM to GFR exchange service + its dedicated (unauthenticated) HttpClient.
        services.AddScoped<IGfrTokenService, GfrTokenService>();
        services.AddHttpClient(GfrTokenService.HttpClientName, (serviceProvider, client) =>
        {
            DownstreamServicesOptions options = serviceProvider.GetRequiredService<IOptions<DownstreamServicesOptions>>().Value;
            if (!string.IsNullOrEmpty(options.GoFileRoom))
            {
                client.BaseAddress = new Uri(options.GoFileRoom.TrimEnd('/') + "/");
            }
        });

        // EM V1 typed client (attaches the exchanged GFR token per request).
        services.AddHttpClient<EngagementManagerClient>((serviceProvider, client) =>
        {
            DownstreamServicesOptions options = serviceProvider.GetRequiredService<IOptions<DownstreamServicesOptions>>().Value;
            if (!string.IsNullOrEmpty(options.EngagementManagerV1))
            {
                client.BaseAddress = new Uri(options.EngagementManagerV1);
            }

            client.DefaultRequestHeaders.TryAddWithoutValidation(HeaderNames.Accept, "application/json");
        });

        services
            .AddMcpServer(options => options.ServerInfo = new() { Name = "cas-mcp-connector", Version = "0.1.0" })
            .WithHttpTransport(options => options.Stateless = true)
            .WithTools<PingTool>()
            .WithTools<EngagementContentsTool>();

        return services;
    }
}
