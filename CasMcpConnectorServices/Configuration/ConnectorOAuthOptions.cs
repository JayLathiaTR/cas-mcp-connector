namespace CasMcpConnectorServices.Configuration;

/// <summary>Options for the connector's mock OAuth authorization server (POC).</summary>
public sealed class ConnectorOAuthOptions
{
    public const string SectionName = "ConnectorOAuth";

    /// <summary>Public issuer/base URL of this connector (e.g. http://localhost:5080). Empty = derive from request.</summary>
    public string IssuerUrl { get; init; } = string.Empty;

    /// <summary>Scopes advertised in the authorization-server metadata.</summary>
    public string[] ScopesSupported { get; init; } = [];
}
