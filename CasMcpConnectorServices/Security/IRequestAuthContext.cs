namespace CasMcpConnectorServices.Security;

/// <summary>Scoped, per-request holder for the caller's authenticated identity (EM/GFR only).</summary>
public interface IRequestAuthContext
{
    bool IsAuthenticated { get; }

    /// <summary>The caller's TR enterprise user id (euid).</summary>
    Guid Euid { get; }

    /// <summary>The raw CIAM access token (used to exchange for a GFR token).</summary>
    string CiamToken { get; }

    /// <summary>The resolved GoFileRoom session token, once set.</summary>
    string? GfrToken { get; }

    void SetCiam(Guid euid, string ciamToken);

    void SetGfrToken(string gfrToken);
}
