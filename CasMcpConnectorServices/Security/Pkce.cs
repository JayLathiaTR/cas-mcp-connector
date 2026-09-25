using System.Security.Cryptography;
using System.Text;

namespace CasMcpConnectorServices.Security;

/// <summary>PKCE (RFC 7636) S256 verification.</summary>
public static class Pkce
{
    /// <summary>Returns true when BASE64URL(SHA256(codeVerifier)) equals the stored code challenge.</summary>
    public static bool Verify(string codeVerifier, string codeChallenge)
    {
        if (string.IsNullOrEmpty(codeVerifier) || string.IsNullOrEmpty(codeChallenge))
        {
            return false;
        }

        string computed = Convert.ToBase64String(SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier)))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(computed),
            Encoding.ASCII.GetBytes(codeChallenge));
    }
}
