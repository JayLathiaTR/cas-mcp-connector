using System.Security.Cryptography;
using CasMcpConnectorServices.Security;
using Microsoft.Extensions.Options;
using Xunit;

namespace CasMcpConnectorServices.Tests;

public class AesGcmRecordEncryptorTests
{
    private static AesGcmRecordEncryptor NewEncryptor()
    {
        string key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        return new AesGcmRecordEncryptor(Options.Create(new TokenEncryptionOptions { Base64EncryptionKey = key }));
    }

    [Fact]
    public void EncryptThenDecrypt_RoundTrips()
    {
        AesGcmRecordEncryptor encryptor = NewEncryptor();
        byte[] dek = encryptor.CreateWrappedDataKey();

        byte[] cipher = encryptor.Encrypt(dek, "gfr-secret-token");
        string plain = encryptor.Decrypt(dek, cipher);

        Assert.Equal("gfr-secret-token", plain);
        Assert.NotEqual("gfr-secret-token", System.Text.Encoding.UTF8.GetString(cipher));
    }

    [Fact]
    public void Decrypt_WithDifferentMasterKey_Throws()
    {
        AesGcmRecordEncryptor a = NewEncryptor();
        AesGcmRecordEncryptor b = NewEncryptor();
        byte[] dek = a.CreateWrappedDataKey();
        byte[] cipher = a.Encrypt(dek, "x");

        Assert.ThrowsAny<CryptographicException>(() => b.Decrypt(dek, cipher));
    }

    [Fact]
    public void Constructor_RejectsNonBase64OrWrongLengthKey()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new AesGcmRecordEncryptor(Options.Create(new TokenEncryptionOptions { Base64EncryptionKey = "" })));
        Assert.Throws<InvalidOperationException>(() =>
            new AesGcmRecordEncryptor(Options.Create(new TokenEncryptionOptions
            {
                Base64EncryptionKey = Convert.ToBase64String(new byte[16]),
            })));
    }
}
