using CasMcpConnectorServices.Auth.OAuth;
using Xunit;

namespace CasMcpConnectorServices.Tests;

public class OAuthCodeStoreTests
{
    [Fact]
    public void SaveThenConsume_ReturnsEntryOnce()
    {
        var store = new OAuthCodeStore();
        var entry = new OAuthCodeStore.CodeEntry("ciam", "challenge", "http://localhost/cb", DateTimeOffset.UtcNow.AddMinutes(5));
        store.Save("code1", entry);

        Assert.True(store.TryConsume("code1", out OAuthCodeStore.CodeEntry got));
        Assert.Equal("ciam", got.CiamToken);

        // Single-use: a second consume fails.
        Assert.False(store.TryConsume("code1", out _));
    }

    [Fact]
    public void Consume_ExpiredCode_Fails()
    {
        var store = new OAuthCodeStore();
        store.Save("old", new OAuthCodeStore.CodeEntry("ciam", "challenge", "http://localhost/cb", DateTimeOffset.UtcNow.AddSeconds(-1)));

        Assert.False(store.TryConsume("old", out _));
    }

    [Fact]
    public void Consume_UnknownCode_Fails()
    {
        var store = new OAuthCodeStore();
        Assert.False(store.TryConsume("nope", out _));
    }
}
