namespace CasMcpConnectorServices.Security;

/// <summary>
/// Options for token envelope encryption. Bound to the <c>TokenEncryption</c> section; the master key
/// value is supplied via AppSecrets (Key Vault per env / local secrets), never hard-coded.
/// </summary>
public sealed class TokenEncryptionOptions
{
    public const string SectionName = "TokenEncryption";

    /// <summary>Base64-encoded 32-byte (AES-256) master key used to wrap per-record data keys.</summary>
    public string Base64EncryptionKey { get; init; } = string.Empty;
}
