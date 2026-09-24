using System.Text.Json.Serialization;

namespace CasMcpConnectorServices.GoFileRoom;

/// <summary>Request body for the CIAM to GFR token exchange.</summary>
public sealed class GfrAuthRequest
{
    [JsonPropertyName("euid")]
    public required string Euid { get; init; }

    [JsonPropertyName("token")]
    public required string Token { get; init; }
}

/// <summary>Response from the CIAM to GFR token exchange.</summary>
public sealed class GfrAuthResponse
{
    [JsonPropertyName("outlookToken")]
    public string? GfrAuthToken { get; init; }

    [JsonPropertyName("login")]
    public string? UserEmail { get; init; }

    [JsonPropertyName("authSuccess")]
    public bool AuthSuccess { get; init; }
}
