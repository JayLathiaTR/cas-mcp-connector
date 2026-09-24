using CasMcpConnectorServices.Tools;
using Xunit;

namespace CasMcpConnectorServices.Tests;

public class PingToolTests
{
    [Fact]
    public void Ping_ReturnsOkStatusAndServiceName()
    {
        PingTool.PingResult result = PingTool.Ping();

        Assert.Equal("cas-mcp-connector", result.Service);
        Assert.Equal("ok", result.Status);
        Assert.False(string.IsNullOrWhiteSpace(result.ServerTimeUtc));
    }
}
