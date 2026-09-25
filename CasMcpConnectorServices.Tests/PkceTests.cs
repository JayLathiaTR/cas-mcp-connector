using CasMcpConnectorServices.Security;
using Xunit;

namespace CasMcpConnectorServices.Tests;

public class PkceTests
{
    // RFC 7636 test vector.
    private const string Verifier = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";
    private const string Challenge = "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM";

    [Fact]
    public void Verify_CorrectVerifier_ReturnsTrue()
    {
        Assert.True(Pkce.Verify(Verifier, Challenge));
    }

    [Fact]
    public void Verify_WrongVerifier_ReturnsFalse()
    {
        Assert.False(Pkce.Verify("not-the-verifier", Challenge));
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("verifier", "")]
    [InlineData("", "challenge")]
    public void Verify_MissingInputs_ReturnsFalse(string verifier, string challenge)
    {
        Assert.False(Pkce.Verify(verifier, challenge));
    }
}
