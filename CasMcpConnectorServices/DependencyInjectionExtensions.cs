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
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using ModelContextProtocol.AspNetCore.Authentication;
using Npgsql;

namespace CasMcpConnectorServices;

/// <summary>Registers the connector's own services, grouped by concern for readability.</summary>
public static class DependencyInjectionExtensions
{
    public static IServiceCollection AddConnectorServices(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddProblemDetails();

        services.AddConnectorAuth(configuration);
        services.AddTokenStore(configuration);
        services.AddDownstreamClients(configuration);
        services.AddConnectorMcp();

        return services;
    }

    /// <summary>Mock OAuth authorization server (login/consent) + per-request auth context.</summary>
    private static IServiceCollection AddConnectorAuth(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ConnectorOAuthOptions>(configuration.GetSection(ConnectorOAuthOptions.SectionName));
        services.AddSingleton<OAuthCodeStore>();
        services.AddScoped<IRequestAuthContext, RequestAuthContext>();

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

        return services;
    }

    /// <summary>Encrypted GFR-token store (Postgres) + AES-256-GCM record-DEK envelope encryption.</summary>
    private static IServiceCollection AddTokenStore(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<TokenEncryptionOptions>(configuration.GetSection(TokenEncryptionOptions.SectionName));
        services.AddSingleton<IRecordEncryptor, AesGcmRecordEncryptor>();

        NpgsqlDataSource dataSource = new NpgsqlDataSourceBuilder(
            configuration.ObtainPostgresqlConnectionString(KnownDatabaseServerNames.PrimaryDb)).Build();
        services.AddSingleton(dataSource);
        services.AddDbContext<ConnectorDbContext>((serviceProvider, options) =>
            options.UseNpgsql(serviceProvider.GetRequiredService<NpgsqlDataSource>()));

        return services;
    }

    /// <summary>Downstream HTTP clients: GFR (CIAM to GFR exchange) and Engagement Manager V1.</summary>
    private static IServiceCollection AddDownstreamClients(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<DownstreamServicesOptions>(configuration.GetSection(DownstreamServicesOptions.SectionName));
        services.AddScoped<IGfrTokenService, GfrTokenService>();

        services.AddHttpClient(GfrTokenService.HttpClientName, (serviceProvider, client) =>
        {
            DownstreamServicesOptions options = serviceProvider.GetRequiredService<IOptions<DownstreamServicesOptions>>().Value;
            if (!string.IsNullOrEmpty(options.GoFileRoom))
            {
                client.BaseAddress = new Uri(options.GoFileRoom.TrimEnd('/') + "/");
            }
        });

        services.AddHttpClient<EngagementManagerClient>((serviceProvider, client) =>
        {
            DownstreamServicesOptions options = serviceProvider.GetRequiredService<IOptions<DownstreamServicesOptions>>().Value;
            if (!string.IsNullOrEmpty(options.EngagementManagerV1))
            {
                client.BaseAddress = new Uri(options.EngagementManagerV1);
            }

            client.DefaultRequestHeaders.TryAddWithoutValidation(HeaderNames.Accept, "application/json");
        });

        return services;
    }

    /// <summary>MCP server (stateless HTTP transport) + the connector's tools.</summary>
    private static IServiceCollection AddConnectorMcp(this IServiceCollection services)
    {
        services
            .AddMcpServer(options => options.ServerInfo = new() { Name = "cas-mcp-connector", Version = "0.1.0" })
            .WithHttpTransport(options => options.Stateless = true)
            .WithTools<PingTool>()
            .WithTools<EngagementContentsTool>();

        return services;
    }
}
