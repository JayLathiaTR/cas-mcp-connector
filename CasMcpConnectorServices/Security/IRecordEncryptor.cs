namespace CasMcpConnectorServices.Security;

/// <summary>
/// Record-level envelope encryption. A per-record data key (DEK) — wrapped by the master key — encrypts
/// the row's sensitive columns. AES-256-GCM throughout.
/// </summary>
public interface IRecordEncryptor
{
    /// <summary>Creates a fresh record data key and returns it wrapped by the master key.</summary>
    byte[] CreateWrappedDataKey();

    /// <summary>Encrypts <paramref name="plaintext"/> with the record's (wrapped) data key.</summary>
    byte[] Encrypt(byte[] wrappedDataKey, string plaintext);

    /// <summary>Decrypts <paramref name="ciphertext"/> with the record's (wrapped) data key.</summary>
    string Decrypt(byte[] wrappedDataKey, byte[] ciphertext);
}
