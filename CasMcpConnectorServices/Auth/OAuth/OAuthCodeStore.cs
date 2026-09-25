using System.Collections.Concurrent;

namespace CasMcpConnectorServices.Auth.OAuth;

/// <summary>
/// In-memory store of issued authorization codes (POC only). Each code maps to the CIAM token the
/// user supplied on the mock login page plus the PKCE challenge and redirect it was bound to.
/// </summary>
public sealed class OAuthCodeStore
{
    private readonly ConcurrentDictionary<string, CodeEntry> _codes = new(StringComparer.Ordinal);

    public void Save(string code, CodeEntry entry) => _codes[code] = entry;

    /// <summary>Removes and returns the entry if present and unexpired; otherwise false.</summary>
    public bool TryConsume(string code, out CodeEntry entry)
    {
        if (_codes.TryRemove(code, out CodeEntry? found) && found.ExpiresUtc > DateTimeOffset.UtcNow)
        {
            entry = found;
            return true;
        }

        entry = null!;
        return false;
    }

    public sealed record CodeEntry(string CiamToken, string CodeChallenge, string RedirectUri, DateTimeOffset ExpiresUtc);
}
