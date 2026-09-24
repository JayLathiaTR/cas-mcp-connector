using CasMcpConnectorServices.Configuration;
using CasMcpConnectorServices.EngagementManager;
using CasMcpConnectorServices.Tools;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace CasMcpConnectorServices;

/// <summary>Registers the connector's own services (config, downstream clients, MCP tools).</summary>
public static class DependencyInjectionExtensions
{
    public static IServiceCollection AddConnectorServices(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddProblemDetails();
        services.AddHttpContextAccessor();

        services.Configure<DownstreamServicesOptions>(configuration.GetSection(DownstreamServicesOptions.SectionName));

        // Typed EM V1 client. P0 shim (forward inbound Authorization) still applies until P1b swaps it
        // for the CIAM→GFR exchange.
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
