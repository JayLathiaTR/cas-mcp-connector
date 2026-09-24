namespace CasMcpConnectorServices.GoFileRoom;

/// <summary>Resolves the GoFileRoom session token for a CIAM user, backed by the encrypted DB store.</summary>
public interface IGfrTokenService
{
    /// <summary>Returns the stored GFR token, exchanging and persisting one when none exists.</summary>
    Task<string> GetOrCreateAsync(Guid euid, string ciamToken, CancellationToken cancellationToken);

    /// <summary>Forces a fresh CIAM to GFR exchange and updates the store (used on a downstream 401).</summary>
    Task<string> RefreshAsync(Guid euid, string ciamToken, CancellationToken cancellationToken);
}
