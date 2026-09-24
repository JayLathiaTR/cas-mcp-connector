using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace CasMcpConnectorServices.Security;

/// <summary>
/// AES-256-GCM record-level envelope implementation of <see cref="IRecordEncryptor"/>. A 256-bit data
/// key (DEK) is wrapped by the master key; each encrypted blob is packed as nonce | tag | ciphertext.
/// </summary>
public sealed class AesGcmRecordEncryptor : IRecordEncryptor
{
    private const int KeySizeBytes = 32; // AES-256
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly byte[] _masterKey;

    public AesGcmRecordEncryptor(IOptions<TokenEncryptionOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _masterKey = DecodeMasterKey(options.Value.Base64EncryptionKey);
    }

    public byte[] CreateWrappedDataKey()
    {
        byte[] dataKey = RandomNumberGenerator.GetBytes(KeySizeBytes);
        try
        {
            return EncryptGcm(_masterKey, dataKey);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
        }
    }

    public byte[] Encrypt(byte[] wrappedDataKey, string plaintext)
    {
        ArgumentNullException.ThrowIfNull(wrappedDataKey);
        ArgumentNullException.ThrowIfNull(plaintext);

        byte[] dataKey = DecryptGcm(_masterKey, wrappedDataKey);
        try
        {
            return EncryptGcm(dataKey, Encoding.UTF8.GetBytes(plaintext));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
        }
    }

    public string Decrypt(byte[] wrappedDataKey, byte[] ciphertext)
    {
        ArgumentNullException.ThrowIfNull(wrappedDataKey);
        ArgumentNullException.ThrowIfNull(ciphertext);

        byte[] dataKey = DecryptGcm(_masterKey, wrappedDataKey);
        try
        {
            return Encoding.UTF8.GetString(DecryptGcm(dataKey, ciphertext));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
        }
    }

    private static byte[] EncryptGcm(byte[] key, byte[] plaintext)
    {
        byte[] nonce = RandomNumberGenerator.GetBytes(NonceSize);
        byte[] cipher = new byte[plaintext.Length];
        byte[] tag = new byte[TagSize];

        using (var aes = new AesGcm(key, TagSize))
        {
            aes.Encrypt(nonce, plaintext, cipher, tag);
        }

        byte[] result = new byte[NonceSize + TagSize + cipher.Length];
        Buffer.BlockCopy(nonce, 0, result, 0, NonceSize);
        Buffer.BlockCopy(tag, 0, result, NonceSize, TagSize);
        Buffer.BlockCopy(cipher, 0, result, NonceSize + TagSize, cipher.Length);
        return result;
    }

    private static byte[] DecryptGcm(byte[] key, byte[] packed)
    {
        if (packed.Length < NonceSize + TagSize)
        {
            throw new CryptographicException("Ciphertext is too short to contain a nonce and tag.");
        }

        ReadOnlySpan<byte> nonce = packed.AsSpan(0, NonceSize);
        ReadOnlySpan<byte> tag = packed.AsSpan(NonceSize, TagSize);
        ReadOnlySpan<byte> cipher = packed.AsSpan(NonceSize + TagSize);

        byte[] plaintext = new byte[cipher.Length];
        using (var aes = new AesGcm(key, TagSize))
        {
            aes.Decrypt(nonce, cipher, tag, plaintext);
        }

        return plaintext;
    }

    private static byte[] DecodeMasterKey(string base64Key)
    {
        if (string.IsNullOrWhiteSpace(base64Key))
        {
            throw new InvalidOperationException($"'{TokenEncryptionOptions.SectionName}:{nameof(TokenEncryptionOptions.Base64EncryptionKey)}' is required.");
        }

        byte[] key;
        try
        {
            key = Convert.FromBase64String(base64Key);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException($"'{TokenEncryptionOptions.SectionName}:{nameof(TokenEncryptionOptions.Base64EncryptionKey)}' must be valid base64.", ex);
        }

        return key.Length != KeySizeBytes
            ? throw new InvalidOperationException($"'{TokenEncryptionOptions.SectionName}:{nameof(TokenEncryptionOptions.Base64EncryptionKey)}' must decode to {KeySizeBytes} bytes (AES-256).")
            : key;
    }
}
